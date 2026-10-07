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

    private readonly SettingsStore _store;
    private readonly Dispatcher _ui;
    private readonly DispatcherTimer _tick;
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private readonly Thread _worker;
    private readonly AutoResetEvent _signal = new(false);
    private readonly object _frameLock = new();
    private SnackbarPresenter? _snackbar;

    private ICamera? _camera;
    private Action<CameraFrame>? _frameHandler;
    private Action<string>? _errorHandler;

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
        _adjustments = Settings.Adjustments ?? ImageAdjustments.Default;
        _isPanelCollapsed = Settings.Window.PanelCollapsed;
        store.SaveFailed += ex => Notify("Не удалось сохранить настройки", ex.Message, NotifyKind.Error);

        CameraPanel = new CameraPanelViewModel(this);
        ColorPanel = new ColorPanelViewModel(this);
        ScalePanel = new ScalePanelViewModel(this);
        MeasurePanel = new MeasurePanelViewModel(this);
        CapturePanel = new CapturePanelViewModel(this);
        Gallery = new GalleryViewModel(this);

        _worker = new Thread(ProcessLoop) { IsBackground = true, Name = "Обработка кадров" };
        _worker.Start();
        _tick = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => OnTick(), _ui);
        _tick.Stop();
        CompositionTarget.Rendering += OnRendering;
    }

    // ───── Контракт хоста ─────

    public Window Owner { get; }
    public AppSettings Settings { get; }
    public string SettingsPath => _store.Path;
    public void SaveSettingsSoon() => _store.ScheduleSave(Settings);

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

    public CameraFrame? LatestProcessedFrame { get { lock (_frameLock) return _latestProcessed; } }
    /// <summary>Сырой кадр, из которого получен LatestProcessedFrame (при паузе — замороженный).</summary>
    public CameraFrame? LatestRawFrame { get { lock (_frameLock) return _latestRaw; } }

    /// <summary>Фоновый поток, каждый принятый кадр. В UI — только Dispatcher.BeginInvoke.</summary>
    public event Action<CameraFrame>? RawFrameReady;
    /// <summary>Фоновый поток, после обработки. В UI — только Dispatcher.BeginInvoke.</summary>
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
        ConnectionState.Connecting => "Ищу Lumenera INFINITY на USB.",
        ConnectionState.Lost => "Кадры не приходят больше 3 секунд. Проверьте кабель — программа переподключится сама.",
        ConnectionState.NoDrivers => "Установите драйверы камеры или попробуйте программу в демо-режиме.",
        _ => "Подключите INFINITY 4 по USB — программа найдёт её сама (опрос раз в 3 с).",
    };

    public void AttachSnackbar(SnackbarPresenter presenter) => _snackbar = presenter;

    /// <summary>Всплывающее уведомление (Snackbar). Можно звать из любого потока.</summary>
    public void Notify(string title, string message, NotifyKind kind = NotifyKind.Info)
    {
        if (!_ui.CheckAccess()) { _ui.BeginInvoke(() => Notify(title, message, kind)); return; }
        if (_snackbar is null) return;
        var sb = new Snackbar(_snackbar)
        {
            Title = title,
            Content = message,
            Appearance = kind switch
            {
                NotifyKind.Success => ControlAppearance.Success,
                NotifyKind.Warning => ControlAppearance.Caution,
                NotifyKind.Error => ControlAppearance.Danger,
                _ => ControlAppearance.Secondary,
            },
            Icon = new SymbolIcon(kind switch
            {
                NotifyKind.Success => SymbolRegular.CheckmarkCircle24,
                NotifyKind.Warning => SymbolRegular.Warning24,
                NotifyKind.Error => SymbolRegular.ErrorCircle24,
                _ => SymbolRegular.Info24,
            }),
            Timeout = TimeSpan.FromSeconds(kind == NotifyKind.Error ? 6 : 3.5),
        };
        _ = sb.ShowAsync();
    }

    // ───── Жизненный цикл камеры ─────

    /// <summary>Старт: драйверы → реальная камера, иначе экран «нет камеры»/«нет драйверов».</summary>
    public async Task InitializeAsync()
    {
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
            if (!CameraFactory.IsDriverAvailable())
            {
                await CloseCameraAsync();
                State = ConnectionState.NoDrivers;
                if (userInitiated) Notify("Драйверы не найдены", "Без драйверов можно работать в демо-режиме.", NotifyKind.Warning);
                return;
            }

            var count = await Task.Run(CameraFactory.CountRealCameras);
            if (count <= 0)
            {
                if (State is not (ConnectionState.Streaming or ConnectionState.Demo) || userInitiated)
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
            Subscribe(cam);
            try
            {
                await Task.Run(() => { cam.Open(); cam.Start(); });
            }
            catch (Exception ex)
            {
                Unsubscribe(cam);
                await Task.Run(cam.Dispose);
                State = ConnectionState.NoCamera;
                Notify("Не удалось подключить камеру", ex.Message, NotifyKind.Error);
                return;
            }
            SetCamera(cam, ConnectionState.Streaming);
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
            await CloseCameraAsync();
            State = ConnectionState.Connecting;
            var cam = CameraFactory.CreateSimulated();
            Subscribe(cam);
            await Task.Run(cam.Start);
            SetCamera(cam, ConnectionState.Demo);
        }
        catch (Exception ex)
        {
            State = ConnectionState.NoCamera;
            Notify("Демо-режим не запустился", ex.Message, NotifyKind.Error);
        }
        finally
        {
            _switchLock.Release();
        }
    }

    private void SetCamera(ICamera cam, ConnectionState state)
    {
        _camera = cam;
        Volatile.Write(ref _lastFrameTicks, Environment.TickCount64);
        State = state;
        OnPropertyChanged(nameof(Camera));
        OnPropertyChanged(nameof(CameraTitle));
        CameraChanged?.Invoke();
    }

    private void Subscribe(ICamera cam)
    {
        _frameHandler = f => OnCameraFrame(cam, f);
        _errorHandler = msg => OnCameraError(msg);
        cam.FrameReceived += _frameHandler;
        cam.Error += _errorHandler;
    }

    private void Unsubscribe(ICamera cam)
    {
        if (_frameHandler is not null) cam.FrameReceived -= _frameHandler;
        if (_errorHandler is not null) cam.Error -= _errorHandler;
        _frameHandler = null;
        _errorHandler = null;
    }

    private async Task CloseCameraAsync()
    {
        var cam = _camera;
        if (cam is null) return;
        Unsubscribe(cam);
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
            && _switchLock.CurrentCount > 0)
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
                if (adj.IsIdentity) processed = raw;
                else
                {
                    var dst = new byte[raw.Bgr24.Length];
                    ImageProcessor.Apply(raw.Bgr24, dst, raw.Width, raw.Height, adj);
                    processed = new CameraFrame { Bgr24 = dst, Width = raw.Width, Height = raw.Height, Sequence = raw.Sequence, Timestamp = raw.Timestamp };
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
                if (_isFrozen && !ReferenceEquals(raw, _frozenRaw)) continue;
                _latestRaw = raw;
                _latestProcessed = processed;
                _renderPending = processed;
            }
            try { ProcessedFrameReady?.Invoke(processed); }
            catch (Exception ex) { OnCameraError(ex.Message); }
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        CameraFrame? f;
        lock (_frameLock) { f = _renderPending; _renderPending = null; }
        if (f is null) return;
        if (_bitmap is null || _bitmap.PixelWidth != f.Width || _bitmap.PixelHeight != f.Height)
        {
            _bitmap = new WriteableBitmap(f.Width, f.Height, 96, 96, PixelFormats.Bgr24, null);
            PreviewBitmap = _bitmap;
            FrameWidth = f.Width;
            FrameHeight = f.Height;
        }
        _bitmap.WritePixels(new Int32Rect(0, 0, f.Width, f.Height), f.Bgr24, f.Width * 3, 0);
    }

    // ───── Команды ─────

    [RelayCommand] private Task Retry() => ConnectAsync();
    [RelayCommand] private void Demo() => StartDemo();
    [RelayCommand] private Task InstallDrivers() => DriverStartupFlow.ReinstallAsync(this);
    [RelayCommand] private void ToggleFreeze() => IsFrozen = !IsFrozen;
    [RelayCommand] private void TogglePanel() => IsPanelCollapsed = !IsPanelCollapsed;

    [RelayCommand]
    private async Task ShowAdvanced()
    {
        var host = ContentDialogHost.GetForWindow(Owner);
        if (host is null) return;
        var vm = new AdvancedSettingsViewModel(this);
        var dialog = new ContentDialog(host)
        {
            Title = "Дополнительно",
            Content = new AdvancedSettingsView { DataContext = vm },
            PrimaryButtonText = "Применить",
            CloseButtonText = "Отмена",
            DialogMaxWidth = 560,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await vm.ApplyAsync();
    }

    /// <summary>Сброс всех настроек к значениям по умолчанию (объект Settings остаётся тем же).</summary>
    public void ResetSettings()
    {
        var defaults = new AppSettings();
        foreach (var p in typeof(AppSettings).GetProperties())
            if (p.CanWrite && p.CanRead) p.SetValue(Settings, p.GetValue(defaults));
        Adjustments = ImageAdjustments.Default;
        IsPanelCollapsed = false;
        SaveSettingsSoon();
        Notify("Настройки сброшены", "Часть изменений вступит в силу после перезапуска.", NotifyKind.Success);
    }

    /// <summary>Закрытие окна: остановить камеру и конвейер, сохранить настройки.</summary>
    public void Shutdown()
    {
        _tick.Stop();
        CompositionTarget.Rendering -= OnRendering;
        var cam = _camera;
        _camera = null;
        if (cam is not null)
        {
            Unsubscribe(cam);
            try { cam.Dispose(); } catch { /* при выходе ошибки закрытия не важны */ }
        }
        _disposed = true;
        _signal.Set();
        Settings.Adjustments = _adjustments;
        _store.Dispose(); // Flush без исключений
    }
}
