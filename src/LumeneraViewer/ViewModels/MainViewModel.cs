using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Camera;
using LumeneraViewer.Drivers;
using LumeneraViewer.Imaging;
using LumeneraViewer.Settings;
using LumeneraViewer.UI;
using LumeneraViewer.Views;
using Wpf.Ui.Controls;

namespace LumeneraViewer.ViewModels;

public enum ConnectionState { NoDrivers, NoCamera, Connecting, Streaming, Lost, Demo }
public enum PreviewTool { None, Ruler, Calibrate }
public enum NotifyKind { Info, Success, Warning, Error }

/// <summary>
/// Хост окна: камера, конвейер кадров, состояние подключения, уведомления.
/// Контракт для тасков 03–06 — в interfaces.md («Хост окна»).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int LossTimeoutMs = 3000;
    private const int PollEveryTicks = 6;   // тик 500 мс → опрос раз в 3 с
    private const int RenderIdleFrames = 30; // без новых кадров ~0.5 с — отписка от CompositionTarget.Rendering
    private static readonly TimeSpan CloseCameraTimeout = TimeSpan.FromSeconds(3);

    private readonly SettingsStore _store;
    private readonly Dispatcher _ui;
    private readonly DispatcherTimer _tick;
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private readonly Thread _worker;
    private readonly AutoResetEvent _signal = new(false);
    private readonly object _frameLock = new();
    private readonly FrameBufferPool _pool = new();
    private readonly DispatcherTimer _saveTimer;
    private readonly SaveThrottle _saveThrottle;

    private ICamera? _camera;
    private IDisposable? _cameraSubscription;
    private volatile bool _shuttingDown;
    private bool _installing;          // идёт установка драйверов — автопереподключение выключено
    private int _renderHooked;         // 1 — подписаны на CompositionTarget.Rendering
    private int _renderIdle;

    // Конвейер: «последний побеждает».
    private CameraFrame? _pendingRaw;
    private CameraFrame? _latestRaw;        // сырой кадр, из которого получен _latestProcessed
    private CameraFrame? _latestProcessed;
    private CameraFrame? _renderPending;
    private CameraFrame? _frozenRaw;
    private volatile bool _isFrozen;
    private volatile ImageAdjustments _adjustments;
    private volatile bool _disposed;
    private WriteableBitmap? _bitmap;

    private long _lastFrameTicks;
    private int _receivedFrames;
    private long _fpsWindowStart = Environment.TickCount64;
    private int _tickCount;
    private long _lastErrorNotify;

    public MainViewModel(Window owner, SettingsStore store)
    {
        Owner = owner;
        _store = store;
        _ui = owner.Dispatcher;
        Settings = store.Load();
        _saveThrottle = new SaveThrottle(() => { MagnificationSettings.StoreLive(Settings); _store.ScheduleSave(Settings); });
        _saveTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            _saveTimer!.Stop();
            _saveThrottle.OnTimer();
        }, _ui);
        _saveTimer.Stop();
        _adjustments = Settings.Adjustments ?? ImageAdjustments.Default;
        _isPanelCollapsed = Settings.Window.PanelCollapsed;
        store.SaveFailed += ex => Notify("Не удалось сохранить настройки", ex.Message, NotifyKind.Error);

        SyncMagnifications();
        CameraPanel = new CameraPanelViewModel(this);
        ColorPanel = new ColorPanelViewModel(this);
        ScalePanel = new ScalePanelViewModel(this);
        MeasurePanel = new MeasurePanelViewModel(this);
        CapturePanel = new CapturePanelViewModel(this);
        Gallery = new GalleryViewModel(this);
        SeriesPanel = new SeriesPanelViewModel(this);
        PrimaryCaptureCommand = new RelayCommand(ExecutePrimaryCapture, () => PrimaryTarget.CanExecute(null));
        CapturePanel.CaptureCommand.CanExecuteChanged += (_, _) => PrimaryCaptureCommand.NotifyCanExecuteChanged();
        SeriesPanel.CaptureStepCommand.CanExecuteChanged += (_, _) => PrimaryCaptureCommand.NotifyCanExecuteChanged();
        SeriesPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SeriesPanelViewModel.IsActive) or null or "")
                PrimaryCaptureCommand.NotifyCanExecuteChanged();
        };

        _worker = new Thread(ProcessLoop) { IsBackground = true, Name = "Обработка кадров" };
        _worker.Start();
        _tick = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => OnTick(), _ui);
        _tick.Stop();
    }

    // ───── Контракт хоста ─────

    public Window Owner { get; }
    public AppSettings Settings { get; }
    public string SettingsPath => _store.Path;

    /// <summary>
    /// Сохранить настройки «скоро»: сериализация не чаще раза в ~1 с (троттлинг в UI-потоке),
    /// последнее изменение не теряется — при закрытии окна дописывается. Можно звать из любого потока.
    /// </summary>
    public void SaveSettingsSoon()
    {
        if (!_ui.CheckAccess()) { _ui.BeginInvoke(SaveSettingsSoon); return; }
        if (_shuttingDown) return;
        // Любое изменение живых значений сразу попадает в профиль текущего увеличения.
        MagnificationSettings.StoreLive(Settings);
        if (_saveThrottle.Request()) _saveTimer.Start();
    }

    /// <summary>
    /// Записать настройки на диск немедленно (UI-поток) — для того, что нельзя потерять (калибровка).
    /// Ошибка записи → уведомление и false.
    /// </summary>
    public bool SaveSettingsNow()
    {
        // Уже взведённый троттл снимаем: всё несохранённое уходит в эту запись, а не второй записью через секунду.
        _saveTimer.Stop();
        _saveThrottle.Flush();
        MagnificationSettings.StoreLive(Settings);
        try
        {
            _store.ScheduleSave(Settings);
            _store.Flush();
            return true;
        }
        catch (Exception ex)
        {
            Notify("Настройки не сохранены", ex.Message, NotifyKind.Error);
            return false;
        }
    }

    /// <summary>
    /// UI-поток: объект Settings тот же, но его содержимое заменено целиком (сброс настроек).
    /// Панели, кэширующие данные из настроек (пресеты, объективы, экспорт), должны перечитать их.
    /// </summary>
    public event Action? SettingsReplaced;

    public ICamera? Camera => _camera;
    /// <summary>В UI-потоке, после открытия/закрытия камеры.</summary>
    public event Action? CameraChanged;

    private ConnectionState _state = ConnectionState.Connecting;
    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsPlaceholderVisible));
            OnPropertyChanged(nameof(IsConnecting));
            OnPropertyChanged(nameof(ShowInstallDrivers));
            OnPropertyChanged(nameof(IsDemo));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(PlaceholderTitle));
            OnPropertyChanged(nameof(PlaceholderText));
            OnPropertyChanged(nameof(CameraTitle));
        }
    }

    private int _frameWidth;
    public int FrameWidth { get => _frameWidth; private set { if (SetProperty(ref _frameWidth, value)) OnPropertyChanged(nameof(ResolutionText)); } }
    private int _frameHeight;
    public int FrameHeight { get => _frameHeight; private set { if (SetProperty(ref _frameHeight, value)) OnPropertyChanged(nameof(ResolutionText)); } }

    /// <summary>Коррекция изображения. Сохраняется в настройках; при паузе переобрабатывает замороженный кадр.</summary>
    public ImageAdjustments Adjustments
    {
        get => _adjustments;
        set
        {
            value ??= ImageAdjustments.Default;
            if (_adjustments == value) return;
            _adjustments = value;
            Settings.Adjustments = value;
            SaveSettingsSoon();
            OnPropertyChanged();
            if (_isFrozen)
            {
                lock (_frameLock)
                    if (_frozenRaw is not null) _pendingRaw = _frozenRaw;
                _signal.Set();
            }
        }
    }

    public bool IsFrozen
    {
        get => _isFrozen;
        set
        {
            if (_isFrozen == value) return;
            lock (_frameLock)
            {
                _frozenRaw = value ? _latestRaw : null;
                _pendingRaw = null;
                _isFrozen = value;
            }
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// То, что на экране. Возвращённый кадр выводится из пула и больше никогда не перезаписывается —
    /// его можно держать сколько угодно (например, отдать в экспорт).
    /// </summary>
    public CameraFrame? LatestProcessedFrame
    {
        get
        {
            lock (_frameLock)
            {
                _pool.Escape(_latestProcessed);
                return _latestProcessed;
            }
        }
    }

    /// <summary>
    /// Кадр из ProcessedFrameReady действителен только во время обработчика (буферы конвейера переиспользуются).
    /// Чтобы обработать его позже (Task.Run и т. п.), вызовите RetainFrame внутри обработчика и
    /// освободите результат (Dispose), когда кадр больше не нужен.
    /// </summary>
    public IDisposable RetainFrame(CameraFrame frame) => _pool.Lease(frame);
    /// <summary>Сырой кадр, из которого получен LatestProcessedFrame (при паузе — замороженный).</summary>
    public CameraFrame? LatestRawFrame { get { lock (_frameLock) return _latestRaw; } }

    /// <summary>Фоновый поток, каждый принятый кадр. В UI — только Dispatcher.BeginInvoke.</summary>
    public event Action<CameraFrame>? RawFrameReady;
    /// <summary>Фоновый поток, после обработки. В UI — только Dispatcher.BeginInvoke. Держать дольше обработчика — только через RetainFrame.</summary>
    public event Action<CameraFrame>? ProcessedFrameReady;

    [ObservableProperty] private PreviewTool _activeTool;

    /// <summary>Текущий масштаб превью (1 = 100 %, экранных px на пиксель кадра). Пишет окно.</summary>
    [ObservableProperty] private double _previewZoom = 1;

    public CameraPanelViewModel CameraPanel { get; }
    public ColorPanelViewModel ColorPanel { get; }
    public ScalePanelViewModel ScalePanel { get; }
    public MeasurePanelViewModel MeasurePanel { get; }
    public CapturePanelViewModel CapturePanel { get; }
    public GalleryViewModel Gallery { get; }
    public SeriesPanelViewModel SeriesPanel { get; }

    /// <summary>
    /// Пробел и главная кнопка «Снимок»: в режиме серии — снять текущий шаг, иначе — обычный снимок.
    /// </summary>
    public IRelayCommand PrimaryCaptureCommand { get; }

    private System.Windows.Input.ICommand PrimaryTarget =>
        SeriesPanel.IsActive ? SeriesPanel.CaptureStepCommand : CapturePanel.CaptureCommand;

    private void ExecutePrimaryCapture()
    {
        var target = PrimaryTarget;
        if (target.CanExecute(null)) target.Execute(null);
    }

    // ───── Для окна ─────

    [ObservableProperty] private WriteableBitmap? _previewBitmap;
    [ObservableProperty] private double _fps;

    private bool _isPanelCollapsed;
    public bool IsPanelCollapsed
    {
        get => _isPanelCollapsed;
        set
        {
            if (!SetProperty(ref _isPanelCollapsed, value)) return;
            Settings.Window.PanelCollapsed = value;
            SaveSettingsSoon();
        }
    }

    public bool IsPlaceholderVisible => State is not (ConnectionState.Streaming or ConnectionState.Demo);
    public bool IsConnecting => State == ConnectionState.Connecting;
    public bool ShowInstallDrivers => State == ConnectionState.NoDrivers;
    public bool IsDemo => State == ConnectionState.Demo;
    public string ResolutionText => FrameWidth > 0 ? $"{FrameWidth} × {FrameHeight}" : "—";

    public string StatusText => State switch
    {
        ConnectionState.Streaming => "Подключено",
        ConnectionState.Demo => "Демо-поток",
        ConnectionState.Connecting => "Подключение…",
        ConnectionState.Lost => "Связь потеряна",
        ConnectionState.NoDrivers => "Нет драйверов",
        _ => "Нет камеры",
    };

    public string CameraTitle => _camera?.Info is { } i
        ? (i.IsSimulated ? i.Model : $"{i.Model} · SN {i.Serial}")
        : "Камера не выбрана";

    public string PlaceholderTitle => State switch
    {
        ConnectionState.Connecting => "Подключение к камере…",
        ConnectionState.Lost => "Связь с камерой потеряна",
        ConnectionState.NoDrivers => "Драйверы камеры не найдены",
        _ => "Камера не подключена",
    };

    public string PlaceholderText => State switch
    {
        ConnectionState.Connecting => "Ищу камеру Lumenera на USB.",
        ConnectionState.Lost => "Кадры не приходят больше 3 секунд. Проверьте кабель — программа переподключится сама.",
        ConnectionState.NoDrivers => "Установите драйверы камеры или попробуйте программу в демо-режиме.",
        _ => "Подключите камеру Lumenera (INFINITY и др.) по USB — программа найдёт её сама (опрос раз в 3 с).",
    };

    /// <summary>Журнал уведомлений (вместо всплывающих сообщений): окно «Уведомления», значок в шапке.</summary>
    public NotificationLog Notifications { get; } = new();

    /// <summary>Записать уведомление в журнал. Можно звать из любого потока.</summary>
    public void Notify(string title, string message, NotifyKind kind = NotifyKind.Info)
    {
        if (!_ui.CheckAccess()) { _ui.BeginInvoke(() => Notify(title, message, kind)); return; }
        Notifications.Add(new NotificationItem(DateTime.Now, title, message ?? "", kind));
    }

    [RelayCommand] private void ShowNotifications() => NotificationsWindow.ShowFor(this);

    /// <summary>Пункт меню «Установить на компьютер…» виден у скачанного exe, не у установленной копии.</summary>
    public bool CanInstallApp => Install.InstallFlow.CanInstall;

    [RelayCommand] private Task InstallApp() => Install.InstallFlow.InstallAsync(this);

    // ───── Жизненный цикл камеры ─────

    /// <summary>Старт: драйверы → реальная камера, иначе экран «нет камеры»/«нет драйверов».</summary>
    public async Task InitializeAsync()
    {
        // Скачанный exe предлагает установить себя; после установки запускается установленная копия, эта закрывается.
        if (await Install.InstallFlow.OfferAtStartupAsync(this)) return;
        var drivers = await DriverStartupFlow.EnsureDriversAsync(this);
        if (Settings.Camera.PreferDemo) StartDemo();
        else if (drivers) await ConnectCoreAsync(userInitiated: false);
        else if (State is ConnectionState.Connecting) State = ConnectionState.NoDrivers;
        _tick.Start();
    }

    /// <summary>Переподключить реальную камеру (с текущими Settings.Camera).</summary>
    public Task ConnectAsync() => ConnectCoreAsync(userInitiated: true);

    private async Task ConnectCoreAsync(bool userInitiated)
    {
        await _switchLock.WaitAsync();
        try
        {
            if (_shuttingDown) return;
            _installing = false;
            if (!CameraFactory.IsDriverAvailable())
            {
                if (State == ConnectionState.Demo)
                {
                    // Работающий демо-режим не гасим: реальной камеры всё равно нет.
                    if (userInitiated) Notify("Драйверы не найдены", "Демо-режим продолжает работать.", NotifyKind.Warning);
                    return;
                }
                await CloseCameraAsync();
                State = ConnectionState.NoDrivers;
                if (userInitiated) Notify("Драйверы не найдены", "Без драйверов можно работать в демо-режиме.", NotifyKind.Warning);
                return;
            }

            var count = await Task.Run(CameraFactory.CountRealCameras);
            if (count <= 0)
            {
                // Демо-режим при отсутствии реальной камеры не гасим; потоковую камеру — только по команде.
                if (State != ConnectionState.Demo && (State != ConnectionState.Streaming || userInitiated))
                {
                    await CloseCameraAsync();
                    State = ConnectionState.NoCamera;
                }
                if (userInitiated) Notify("Камера не найдена", "Проверьте USB-кабель и питание камеры.", NotifyKind.Warning);
                return;
            }

            await CloseCameraAsync();
            State = ConnectionState.Connecting;
            var cam = CameraFactory.CreateReal(Settings.Camera);
            var sub = Subscribe(cam);
            try
            {
                await Task.Run(() => { cam.Open(); cam.Start(); });
            }
            catch (Exception ex)
            {
                sub.Dispose();
                await Task.Run(cam.Dispose);
                if (_shuttingDown) return;
                State = ConnectionState.NoCamera;
                Notify("Не удалось подключить камеру", ex.Message, NotifyKind.Error);
                return;
            }
            if (!TrySetCamera(cam, sub, ConnectionState.Streaming)) return;
            Notify("Камера подключена", CameraTitle, NotifyKind.Success);
        }
        finally
        {
            _switchLock.Release();
        }
    }

    /// <summary>Перейти на симулятор.</summary>
    public void StartDemo() => _ = StartDemoAsync();

    private async Task StartDemoAsync()
    {
        await _switchLock.WaitAsync();
        try
        {
            if (_shuttingDown) return;
            _installing = false;
            await CloseCameraAsync();
            State = ConnectionState.Connecting;
            var cs = Settings.Camera ?? new CameraSettings();
            var cam = CameraFactory.CreateSimulated(cs.DemoWidth, cs.DemoHeight);
            var sub = Subscribe(cam);
            try { await Task.Run(cam.Start); }
            catch
            {
                sub.Dispose();
                cam.Dispose();
                throw;
            }
            TrySetCamera(cam, sub, ConnectionState.Demo);
        }
        catch (Exception ex)
        {
            if (_shuttingDown) return;
            State = ConnectionState.NoCamera;
            Notify("Демо-режим не запустился", ex.Message, NotifyKind.Error);
        }
        finally
        {
            _switchLock.Release();
        }
    }

    /// <summary>
    /// Установить открытую камеру. Если окно уже закрывается (Shutdown прошёл, пока шло Open/Start),
    /// камера не устанавливается, а закрывается в фоне. false — не установлена.
    /// </summary>
    private bool TrySetCamera(ICamera cam, IDisposable subscription, ConnectionState state)
    {
        if (_shuttingDown)
        {
            subscription.Dispose();
            _ = Task.Run(() => { try { cam.Dispose(); } catch { /* выход */ } });
            return false;
        }
        _camera = cam;
        _cameraSubscription = subscription;
        Volatile.Write(ref _lastFrameTicks, Environment.TickCount64);
        State = state;
        OnPropertyChanged(nameof(Camera));
        OnPropertyChanged(nameof(CameraTitle));
        CameraChanged?.Invoke();
        return true;
    }

    /// <summary>Подписка на события конкретной камеры; Dispose отписывает именно эти обработчики.</summary>
    private IDisposable Subscribe(ICamera cam) => new CameraSubscription(cam, f => OnCameraFrame(cam, f), OnCameraError);

    private sealed class CameraSubscription : IDisposable
    {
        private ICamera? _cam;
        private readonly Action<CameraFrame> _frame;
        private readonly Action<string> _error;

        public CameraSubscription(ICamera cam, Action<CameraFrame> frame, Action<string> error)
        {
            _cam = cam;
            _frame = frame;
            _error = error;
            cam.FrameReceived += frame;
            cam.Error += error;
        }

        public void Dispose()
        {
            var cam = Interlocked.Exchange(ref _cam, null);
            if (cam is null) return;
            cam.FrameReceived -= _frame;
            cam.Error -= _error;
        }
    }

    private async Task CloseCameraAsync()
    {
        var cam = _camera;
        if (cam is null) return;
        _cameraSubscription?.Dispose();
        _cameraSubscription = null;
        _camera = null;
        lock (_frameLock) _pendingRaw = null;
        try { await Task.Run(cam.Dispose); }
        catch (Exception ex) { Notify("Ошибка при закрытии камеры", ex.Message, NotifyKind.Warning); }
        Fps = 0;
        OnPropertyChanged(nameof(Camera));
        OnPropertyChanged(nameof(CameraTitle));
        CameraChanged?.Invoke();
    }

    private void OnCameraError(string message)
    {
        // Не чаще раза в 5 с, чтобы ошибка на каждом кадре не заваливала экран.
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastErrorNotify) < 5000) return;
        Interlocked.Exchange(ref _lastErrorNotify, now);
        Notify("Ошибка камеры", message, NotifyKind.Error);
    }

    private void OnTick()
    {
        _tickCount++;
        var now = Environment.TickCount64;

        if (now - _fpsWindowStart >= 1000)
        {
            var frames = Interlocked.Exchange(ref _receivedFrames, 0);
            Fps = _camera is null ? 0 : Math.Round(frames * 1000.0 / (now - _fpsWindowStart), 1);
            _fpsWindowStart = now;
        }

        if (State == ConnectionState.Streaming && _camera is not null
            && now - Volatile.Read(ref _lastFrameTicks) > LossTimeoutMs && _switchLock.CurrentCount > 0)
        {
            _ = OnLostAsync();
            return;
        }

        if (_tickCount % PollEveryTicks == 0
            && State is ConnectionState.NoCamera or ConnectionState.NoDrivers or ConnectionState.Lost
            && !_installing && _switchLock.CurrentCount > 0)
        {
            _ = PollAsync();
        }
    }

    private async Task OnLostAsync()
    {
        await _switchLock.WaitAsync();
        try
        {
            if (State != ConnectionState.Streaming) return;
            await CloseCameraAsync();
            State = ConnectionState.Lost;
            Notify("Связь с камерой потеряна", "Жду камеру и подключусь автоматически.", NotifyKind.Warning);
        }
        finally
        {
            _switchLock.Release();
        }
    }

    private async Task PollAsync()
    {
        var found = await Task.Run(() => CameraFactory.IsDriverAvailable() && CameraFactory.CountRealCameras() > 0);
        if (_installing || _shuttingDown) return;
        if (found && State is ConnectionState.NoCamera or ConnectionState.NoDrivers or ConnectionState.Lost)
            await ConnectCoreAsync(userInitiated: false);
        else if (!found && State == ConnectionState.NoDrivers && CameraFactory.IsDriverAvailable())
            State = ConnectionState.NoCamera;
    }

    // ───── Конвейер кадров ─────

    private void OnCameraFrame(ICamera source, CameraFrame frame)
    {
        if (!ReferenceEquals(source, _camera) && _camera is not null) return; // запоздалый кадр старой камеры
        Volatile.Write(ref _lastFrameTicks, Environment.TickCount64);
        Interlocked.Increment(ref _receivedFrames);
        try { RawFrameReady?.Invoke(frame); }
        catch (Exception ex) { OnCameraError(ex.Message); }
        if (_isFrozen) return;
        lock (_frameLock) _pendingRaw = frame;
        _signal.Set();
    }

    private void ProcessLoop()
    {
        while (!_disposed)
        {
            _signal.WaitOne();
            if (_disposed) break;
            CameraFrame? raw;
            lock (_frameLock) { raw = _pendingRaw; _pendingRaw = null; }
            if (raw is null) continue;

            var adj = _adjustments;
            CameraFrame processed;
            try
            {
                if (adj.IsIdentity) processed = raw;   // не из пула: неизменяемый кадр камеры
                else
                {
                    // Буфер из пула (счётчик 1 — ссылка «последний кадр»); свободен только когда все ссылки отпущены.
                    processed = _pool.Rent(raw.Width, raw.Height, raw.Sequence, raw.Timestamp);
                    try { ImageProcessor.Apply(raw.Bgr24, processed.Bgr24, raw.Width, raw.Height, adj); }
                    catch { _pool.Release(processed); throw; }
                }
            }
            catch (Exception ex)
            {
                OnCameraError("Ошибка обработки кадра: " + ex.Message);
                continue;
            }

            lock (_frameLock)
            {
                // Кадр, пришедший до паузы, не должен заменить замороженный.
                if (_isFrozen && !ReferenceEquals(raw, _frozenRaw)) { _pool.Release(processed); continue; }
                _latestRaw = raw;
                _pool.Release(_latestProcessed);
                _latestProcessed = processed;
                _pool.AddRef(processed);              // ссылка «ждёт отрисовки»
                _pool.Release(_renderPending);
                _renderPending = processed;
            }
            if (Interlocked.CompareExchange(ref _renderHooked, 1, 0) == 0) _ui.BeginInvoke(HookRendering);
            // Пока идёт обработчик, кадр остаётся «последним»: заменить его может только этот же поток.
            try { ProcessedFrameReady?.Invoke(processed); }
            catch (Exception ex) { OnCameraError(ex.Message); }
        }
    }

    // CompositionTarget.Rendering подписан, только пока идут кадры: подписка — при первом кадре,
    // отписка — после RenderIdleFrames отрисовок без новых кадров.
    private void HookRendering()
    {
        if (_shuttingDown) return;
        _renderIdle = 0;
        CompositionTarget.Rendering -= OnRendering;
        CompositionTarget.Rendering += OnRendering;
    }

    private void UnhookRendering()
    {
        CompositionTarget.Rendering -= OnRendering;
        Volatile.Write(ref _renderHooked, 0);
        bool pending;
        lock (_frameLock) pending = _renderPending is not null;
        // Кадр мог прийти между проверкой и отпиской — подписаться снова.
        if (pending && !_shuttingDown && Interlocked.CompareExchange(ref _renderHooked, 1, 0) == 0) HookRendering();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        CameraFrame? f;
        lock (_frameLock) { f = _renderPending; _renderPending = null; }   // ссылка «ждёт отрисовки» переходит сюда
        if (f is null)
        {
            if (++_renderIdle >= RenderIdleFrames) UnhookRendering();
            return;
        }
        _renderIdle = 0;
        try
        {
            if (_bitmap is null || _bitmap.PixelWidth != f.Width || _bitmap.PixelHeight != f.Height)
            {
                _bitmap = new WriteableBitmap(f.Width, f.Height, 96, 96, PixelFormats.Bgr24, null);
                PreviewBitmap = _bitmap;
                FrameWidth = f.Width;
                FrameHeight = f.Height;
            }
            _bitmap.WritePixels(new Int32Rect(0, 0, f.Width, f.Height), f.Bgr24, f.Width * 3, 0);
        }
        finally
        {
            _pool.Release(f);
        }
    }

    // ───── Команды ─────

    [RelayCommand] private Task Retry() => ConnectAsync();
    [RelayCommand] private void Demo() => StartDemo();
    [RelayCommand] private Task InstallDrivers() => DriverStartupFlow.ReinstallAsync(this);
    [RelayCommand] private void ToggleFreeze() => IsFrozen = !IsFrozen;
    [RelayCommand] private void TogglePanel() => IsPanelCollapsed = !IsPanelCollapsed;
    [RelayCommand] private Task ShowConfigProfiles() => ConfigProfilesViewModel.ShowAsync(this);

    [RelayCommand]
    private async Task ShowAdvanced()
    {
        var vm = new AdvancedSettingsViewModel(this);
        var dialog = new ContentDialog
        {
            Title = "Дополнительно",
            Content = new AdvancedSettingsView { DataContext = vm },
            PrimaryButtonText = "Применить",
            CloseButtonText = "Отмена",
        };
        if (await Dialogs.ShowAsync(Owner, dialog, 560) == ContentDialogResult.Primary) await vm.ApplyAsync();
    }

    /// <summary>Сброс всех настроек к значениям по умолчанию (объект Settings остаётся тем же).</summary>
    public void ResetSettings()
    {
        ReplaceSettings(new AppSettings());
        Notify("Настройки сброшены", "Часть изменений вступит в силу после перезапуска.", NotifyKind.Success);
    }

    /// <summary>
    /// Заменить все настройки загруженной конфигурацией без перезапуска (UI-поток): живой цвет,
    /// экспозиция, усиление, плашка и увеличения применяются сразу, панели перечитывают всё по SettingsReplaced.
    /// Настройки подключения камеры вступят в силу при следующем подключении.
    /// </summary>
    public void LoadConfiguration(AppSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        ReplaceSettings(s);
    }

    private void ReplaceSettings(AppSettings source)
    {
        _ui.VerifyAccess();   // коллекции и привязки — только из UI-потока
        Settings.CopyFrom(source);
        IsPanelCollapsed = Settings.Window?.PanelCollapsed ?? false;
        SyncMagnifications();
        ApplyLive();
        SettingsReplaced?.Invoke();   // панели перечитывают профили, плашку, экспорт
        OnPropertyChanged(nameof(CurrentMagnification));
        OnPropertyChanged(nameof(IsCurrentCalibrated));
        MagnificationChanged?.Invoke(Settings.CurrentMagnification);
        SaveSettingsSoon();
    }

    // ───── Увеличения ─────

    private readonly ObservableCollection<int> _magnifications = new();

    /// <summary>Увеличения, у которых есть профиль, по возрастанию (ObservableCollection).</summary>
    public IReadOnlyList<int> AvailableMagnifications => _magnifications;

    /// <summary>UI-поток, после того как профиль нового увеличения применён.</summary>
    public event Action<int>? MagnificationChanged;

    /// <summary>
    /// Текущее увеличение. Установка: живые значения → профиль старого, профиль нового → живые
    /// (цвет, экспозиция и усиление камеры, длина плашки, мкм/px); превью и плашка обновляются сразу.
    /// Неизвестное увеличение получает новый профиль; вне 1–10000 — игнорируется.
    /// </summary>
    public int CurrentMagnification
    {
        get => Settings.CurrentMagnification;
        set
        {
            if (!_ui.CheckAccess()) { _ui.BeginInvoke(() => CurrentMagnification = value); return; }
            if (!MagnificationSettings.IsValid(value) || value == Settings.CurrentMagnification) return;
            MagnificationSettings.StoreLive(Settings);
            MagnificationSettings.LoadLive(Settings, value);
            SyncMagnifications();
            ApplyLive();
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCurrentCalibrated));
            MagnificationChanged?.Invoke(value);
            SaveSettingsSoon();
        }
    }

    /// <summary>У текущего увеличения есть калибровка мкм/px.</summary>
    public bool IsCurrentCalibrated => MagnificationSettings.CurrentMicronsPerPixel(Settings) is > 0;

    /// <summary>Добавить профиль увеличения (нейтральный); уже существующее не меняется. Не переключает.</summary>
    public void AddMagnification(int mag)
    {
        if (!MagnificationSettings.IsValid(mag)) return;
        MagnificationSettings.GetOrCreate(Settings, mag);
        SyncMagnifications();
        SaveSettingsSoon();
    }

    /// <summary>Удалить профиль увеличения; текущее и последнее не удаляются (false).</summary>
    public bool RemoveMagnification(int mag)
    {
        if (!MagnificationSettings.Remove(Settings, mag)) return false;
        SyncMagnifications();
        SaveSettingsSoon();
        return true;
    }

    /// <summary>Калибровка или длина плашки текущего увеличения поменялась (зовёт ScalePanel).</summary>
    public void NotifyCalibrationChanged() => OnPropertyChanged(nameof(IsCurrentCalibrated));

    /// <summary>Применить профиль цвета к текущему увеличению (экспозиция и усиление — если они в профиле).</summary>
    public void ApplyColorProfile(ColorProfile p)
    {
        ArgumentNullException.ThrowIfNull(p);
        Adjustments = p.Adjustments ?? ImageAdjustments.Default;
        if (p.IncludesExposure)
        {
            if (p.ExposureMs is { } e) Settings.ExposureMs = e;
            if (p.Gain is { } g) Settings.Gain = g;
            CameraPanel.ApplyExposureGain(p.ExposureMs, p.Gain);
        }
        SaveSettingsSoon();
    }

    private void SyncMagnifications()
    {
        MagnificationSettings.GetOrCreate(Settings, Settings.CurrentMagnification);
        var mags = Settings.Magnifications.Select(m => m.Magnification).Distinct().OrderBy(m => m).ToList();
        if (_magnifications.SequenceEqual(mags)) return;
        // Точечно: переключатель перестраивает сегменты на каждое изменение коллекции.
        for (int i = _magnifications.Count - 1; i >= 0; i--)
            if (!mags.Contains(_magnifications[i])) _magnifications.RemoveAt(i);
        for (int i = 0; i < mags.Count; i++)
            if (i >= _magnifications.Count || _magnifications[i] != mags[i]) _magnifications.Insert(i, mags[i]);
    }

    /// <summary>Живые значения из Settings → конвейер, камера, плашка.</summary>
    private void ApplyLive()
    {
        Adjustments = Settings.Adjustments ?? ImageAdjustments.Default;
        CameraPanel.ApplyExposureGain(Settings.ExposureMs, Settings.Gain);
        ScalePanel.Reload();
    }

    /// <summary>
    /// Перед установкой драйверов: закрыть камеру и выключить автопереподключение
    /// (включается снова при ConnectAsync/StartDemo). Возвращает, был ли включён демо-режим.
    /// </summary>
    public async Task<bool> DisconnectForInstallAsync()
    {
        await _switchLock.WaitAsync();
        try
        {
            var wasDemo = State == ConnectionState.Demo;
            _installing = true;
            await CloseCameraAsync();
            if (State is ConnectionState.Streaming or ConnectionState.Demo or ConnectionState.Connecting or ConnectionState.Lost)
                State = ConnectionState.NoCamera;
            return wasDemo;
        }
        finally
        {
            _switchLock.Release();
        }
    }

    /// <summary>
    /// Закрытие окна: остановить камеру (в фоне, ждём не дольше 3 с — окно закрывается в любом случае),
    /// конвейер и сохранить настройки.
    /// </summary>
    public void Shutdown()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;   // камера, открывшаяся после этого (шло Open/Start), не установится, а закроется
        _tick.Stop();
        _saveTimer.Stop();
        CompositionTarget.Rendering -= OnRendering;
        var cam = _camera;
        _camera = null;
        _cameraSubscription?.Dispose();
        _cameraSubscription = null;
        if (cam is not null)
        {
            var closing = Task.Run(() => { try { cam.Dispose(); } catch { /* при выходе ошибки закрытия не важны */ } });
            closing.Wait(CloseCameraTimeout);
        }
        _disposed = true;
        _signal.Set();
        Gallery.Close();
        Settings.Adjustments = _adjustments;
        _saveThrottle.Request();   // финальный снимок: размер окна и всё, что поменялось
        _saveThrottle.Flush();
        _store.Dispose(); // Flush без исключений
    }
}
