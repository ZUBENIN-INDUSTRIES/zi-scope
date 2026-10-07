using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Camera;
using LumeneraViewer.Export;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;

namespace LumeneraViewer.ViewModels;

public sealed record FormatOption(ImageFormatKind Kind, string Name);

/// <summary>
/// Карточка «Сохранение»: настройки экспорта, снимок (кнопка и Пробел), тайм-лапс.
/// Все настройки живут в Settings.Export / Settings.Timelapse.
/// </summary>
public partial class CapturePanelViewModel : ObservableObject
{
    public const double MinInterval = 0.5;

    private readonly FrameExporter _exporter = new();
    private readonly DispatcherTimer _countdown;
    private CancellationTokenSource? _series;
    private DateTime _nextShotAt;

    public MainViewModel Host { get; }
    private ExportSettings E => Host.Settings.Export ??= new ExportSettings();
    private TimelapseSettings TL => Host.Settings.Timelapse ??= new TimelapseSettings();

    public CapturePanelViewModel(MainViewModel host)
    {
        Host = host;
        _countdown = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => UpdateCountdown(), host.Owner.Dispatcher);
        _countdown.Stop();
        host.Owner.Closing += OnOwnerClosing;
    }

    public IReadOnlyList<FormatOption> Formats => AllFormats;

    public static IReadOnlyList<FormatOption> AllFormats { get; } = new[]
    {
        new FormatOption(ImageFormatKind.Png, "PNG — без потерь"),
        new FormatOption(ImageFormatKind.Jpeg, "JPEG"),
        new FormatOption(ImageFormatKind.Tiff, "TIFF"),
        new FormatOption(ImageFormatKind.Bmp, "BMP"),
        new FormatOption(ImageFormatKind.JpegXr, "JPEG XR (.jxr)"),
    };

    // ───── Настройки экспорта ─────

    public FormatOption SelectedFormat
    {
        get => AllFormats.FirstOrDefault(f => f.Kind == E.Format) ?? AllFormats[0];
        set
        {
            if (value is null || value.Kind == E.Format) return;
            E.Format = value.Kind;
            Changed();
            OnPropertyChanged(nameof(IsJpeg));
            OnPropertyChanged(nameof(IsTiff));
        }
    }

    public bool IsJpeg => E.Format == ImageFormatKind.Jpeg;
    public bool IsTiff => E.Format == ImageFormatKind.Tiff;

    public double JpegQuality
    {
        get => E.JpegQuality;
        set { var q = (int)Math.Round(Math.Clamp(value, 50, 100)); if (q != E.JpegQuality) { E.JpegQuality = q; Changed(); } }
    }

    public bool TiffLzw { get => E.TiffLzw; set { if (value != E.TiffLzw) { E.TiffLzw = value; Changed(); } } }

    public string Folder
    {
        get => E.Folder;
        set
        {
            var v = value?.Trim().Trim('"') ?? "";
            if (v == E.Folder) return;
            if (!IsValidFolder(v))
            {
                Host.Notify("Папка не изменена", $"Укажите полный путь к папке, например C:\\Снимки. Введено: «{v}»", NotifyKind.Warning);
                OnPropertyChanged();   // вернуть прежнее значение в поле
                return;
            }
            E.Folder = v;
            Changed();
            Host.Gallery.Reload();
        }
    }

    private static bool IsValidFolder(string path)
    {
        try { return Path.IsPathFullyQualified(path) && path.IndexOfAny(Path.GetInvalidPathChars()) < 0 && Path.GetFullPath(path).Length > 0; }
        catch (Exception) { return false; }
    }

    public string Prefix { get => E.Prefix; set { if (value != E.Prefix) { E.Prefix = value ?? ""; Changed(); } } }

    public bool BurnOverlays { get => E.BurnOverlays; set { if (value != E.BurnOverlays) { E.BurnOverlays = value; Changed(); } } }
    public bool AlsoSaveClean { get => E.AlsoSaveClean; set { if (value != E.AlsoSaveClean) { E.AlsoSaveClean = value; Changed(); } } }
    public bool WriteMetadata { get => E.WriteMetadata; set { if (value != E.WriteMetadata) { E.WriteMetadata = value; Changed(); } } }

    private void Changed([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        Host.SaveSettingsSoon();
    }

    [RelayCommand]
    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Папка для снимков" };
        if (Directory.Exists(E.Folder)) dlg.InitialDirectory = E.Folder;
        if (dlg.ShowDialog(Host.Owner) == true) Folder = dlg.FolderName;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(E.Folder);
            Process.Start(new ProcessStartInfo(E.Folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Host.Notify("Не удалось открыть папку", ex.Message, NotifyKind.Error);
        }
    }

    // ───── Снимок ─────

    /// <summary>Сколько снимков ещё пишется (для индикатора).</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsSaving), nameof(StatusText))] private int _pendingSaves;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText))] private string? _lastSaved;

    public bool IsSaving => PendingSaves > 0;

    public string StatusText => PendingSaves > 0
        ? $"Сохранение… ({PendingSaves})"
        : LastSaved is null ? "Снимок — кнопка внизу или Пробел" : "Последний: " + LastSaved;

    /// <summary>
    /// Кнопка «Снимок» и Пробел. Всегда разрешена: каждое нажатие ставит свой кадр в очередь записи,
    /// поэтому повторные нажатия не теряются.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Capture() => await CaptureCoreAsync(null, notifySuccess: true);

    /// <summary>Сохранить текущий кадр (в паузе — замороженный). null — нет кадра или ошибка.</summary>
    private async Task<string?> CaptureCoreAsync(int? seriesIndex, bool notifySuccess)
    {
        var frame = Host.LatestProcessedFrame;
        if (frame is null)
        {
            Host.Notify("Снимок", "Нет кадра для сохранения", NotifyKind.Warning);
            return null;
        }

        var overlays = E.BurnOverlays ? Host.ScalePanel.BuildOverlayState() : null;
        var meta = BuildMetadata(frame);
        PendingSaves++;
        try
        {
            var path = await _exporter.SaveAsync(frame, overlays, meta, E, seriesIndex);
            LastSaved = Path.GetFileName(path);
            Host.Gallery.Add(path);
            if (notifySuccess) Host.Notify("Снимок сохранён", Path.GetFileName(path), NotifyKind.Success);
            return path;
        }
        catch (Exception ex)
        {
            Host.Notify("Не удалось сохранить снимок", Describe(ex), NotifyKind.Error);
            return null;
        }
        finally
        {
            PendingSaves--;
        }
    }

    private CaptureMetadata BuildMetadata(CameraFrame frame)
    {
        var info = Host.Camera?.Info;
        var time = frame.Timestamp == default ? DateTime.Now : frame.Timestamp;
        // В паузе кадр старый — время файла берём текущее, чтобы повторные снимки не совпадали по имени.
        if (Host.IsFrozen || (DateTime.Now - time).Duration() > TimeSpan.FromSeconds(5)) time = DateTime.Now;
        return new CaptureMetadata(
            time, info?.Model, info?.Serial, info?.IsSimulated ?? false,
            Host.CameraPanel.CurrentExposure, Host.CameraPanel.CurrentGain, Host.Adjustments,
            Host.Settings.CurrentObjective, OverlayState.CurrentMicronsPerPixel(Host.Settings));
    }

    private static string Describe(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Нет доступа к папке. Выберите другую папку. " + ex.Message,
        IOException io when IsDiskFull(io) => "Недостаточно места на диске. " + ex.Message,
        _ => ex.Message,
    } + " Кадр не потерян — можно повторить.";

    private static bool IsDiskFull(IOException ex) => (ex.HResult & 0xFFFF) is 0x70 or 0x27;

    // ───── Тайм-лапс ─────

    public double IntervalSeconds
    {
        get => TL.IntervalSeconds;
        set
        {
            var v = double.IsFinite(value) ? Math.Max(MinInterval, value) : MinInterval;
            if (Math.Abs(v - TL.IntervalSeconds) < 1e-9) { OnPropertyChanged(); return; }
            TL.IntervalSeconds = v;
            Changed();
        }
    }

    public double Count
    {
        get => TL.Count;
        set
        {
            var v = double.IsFinite(value) ? (int)Math.Clamp(Math.Round(value), 0, 1_000_000) : 0;
            if (v == TL.Count) { OnPropertyChanged(); return; }
            TL.Count = v;
            Changed();
            OnPropertyChanged(nameof(ProgressText));
        }
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProgressText), nameof(ProgressFraction))] private int _shotsTaken;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProgressText))] private bool _isRunning;
    [ObservableProperty] private string _nextShotText = "";

    private int _seriesTotal;

    public string ProgressText => IsRunning || ShotsTaken > 0
        ? $"{ShotsTaken} / {(_seriesTotal > 0 ? _seriesTotal : "∞")}"
        : TL.Count > 0 ? $"{TL.Count} кадров" : "без ограничения";

    public double ProgressFraction => _seriesTotal > 0 ? Math.Clamp((double)ShotsTaken / _seriesTotal, 0, 1) : 0;

    [RelayCommand]
    private void ToggleTimelapse()
    {
        if (IsRunning) StopTimelapse(null);
        else _ = RunTimelapseAsync();
    }

    private async Task RunTimelapseAsync()
    {
        var cts = new CancellationTokenSource();
        _series = cts;
        _seriesTotal = TL.Count;
        ShotsTaken = 0;
        IsRunning = true;
        var interval = TimeSpan.FromSeconds(Math.Max(MinInterval, TL.IntervalSeconds));
        var start = DateTime.Now;
        _countdown.Start();
        try
        {
            for (var i = 1; _seriesTotal == 0 || i <= _seriesTotal; i++)
            {
                // Расписание от старта серии: задержки записи не накапливаются.
                _nextShotAt = start + interval * (i - 1);
                var wait = _nextShotAt - DateTime.Now;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cts.Token);
                if (cts.IsCancellationRequested) return;

                if (await CaptureCoreAsync(i, notifySuccess: false) is null)
                {
                    StopTimelapse("Серия остановлена: снимок не сохранён.");
                    return;
                }
                ShotsTaken = i;
            }
            StopTimelapse(null);
            Host.Notify("Тайм-лапс завершён", $"Сохранено кадров: {ShotsTaken}", NotifyKind.Success);
        }
        catch (OperationCanceledException) { /* остановлено пользователем */ }
        finally
        {
            if (ReferenceEquals(_series, cts)) StopTimelapse(null);
            cts.Dispose();
        }
    }

    private void StopTimelapse(string? reason)
    {
        if (_series is { } s && !s.IsCancellationRequested) s.Cancel();
        _series = null;
        _countdown.Stop();
        NextShotText = "";
        if (IsRunning && reason is not null) Host.Notify("Тайм-лапс", reason, NotifyKind.Warning);
        IsRunning = false;
    }

    private void UpdateCountdown()
    {
        if (!IsRunning) return;
        var left = _nextShotAt - DateTime.Now;
        NextShotText = PendingSaves > 0 && left <= TimeSpan.Zero
            ? "запись…"
            : $"следующий через {Math.Max(0, left.TotalSeconds):0.0} с";
    }

    /// <summary>Закрытие окна: остановить серию и дождаться записи начатых снимков.</summary>
    private void OnOwnerClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel) return;
        StopTimelapse(null);
        _exporter.WaitIdle(TimeSpan.FromSeconds(10));
    }
}
