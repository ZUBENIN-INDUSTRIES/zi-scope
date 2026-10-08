using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Camera;
using LumeneraViewer.Export;
using LumeneraViewer.Overlays;
using LumeneraViewer.Series;
using LumeneraViewer.Settings;
using LumeneraViewer.UI;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Вкладка «Серия» и карта шагов: конструктор серии, прогон «Снять шаг», пересъёмка, удаление, пропуск,
/// произвольный номер, продолжение после перезапуска. Логика шагов — в <see cref="SeriesRunner"/>.
/// Контракт: <see cref="IsActive"/> — идёт ли серия; <see cref="CaptureStepCommand"/> — снять текущий шаг.
/// </summary>
public partial class SeriesPanelViewModel : ObservableObject
{
    /// <summary>Ряд увеличений в конструкторе.</summary>
    public static readonly int[] StandardMagnifications = { 100, 200, 500, 1000, 2000 };

    private readonly FrameExporter _exporter = new();
    private readonly Dictionary<SeriesStepKey, SeriesStepItem> _items = new();
    private bool _folderEdited;
    private bool _exporterDisposed;

    public SeriesPanelViewModel(MainViewModel host)
    {
        Host = host;
        LoadDefaults();
        host.SettingsReplaced += () => { if (Runner is null) LoadDefaults(); Refresh(); };
        host.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsCurrentCalibrated) or nameof(MainViewModel.CurrentMagnification)) Refresh();
        };
        // Ctrl+Z во время серии — переснять предыдущий шаг. Не во время съёмки шага, не при открытом диалоге
        // и не в полях ввода (там Ctrl+Z — отмена ввода).
        host.Owner.PreviewKeyDown += (_, e) =>
        {
            if (!Idle || e.Key != Key.Z || Keyboard.Modifiers != ModifierKeys.Control) return;
            if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.PasswordBox) return;
            if (IsDialogOpen) return;
            ReshootPrevious();
            e.Handled = true;
        };
        host.Owner.ContentRendered += async (_, _) => await OfferResumeAsync();
        host.Owner.Closing += OnOwnerClosing;
    }

    public MainViewModel Host { get; }
    private AppSettings S => Host.Settings;

    /// <summary>Идёт ли серия: тогда Пробел и главная кнопка снимают шаг, а внизу показывается карта серии.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDesigning))] private bool _isActive;

    public bool IsDesigning => !IsActive;

    /// <summary>Прогон идущей серии; null — серии нет.</summary>
    public SeriesRunner? Runner { get; private set; }

    // ═════ Конструктор ═════

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SummaryText))] private string _name = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SummaryText))] private double _groups = 16;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SummaryText))] private double _samples = 4;
    [ObservableProperty] private string _customMagText = "";
    [ObservableProperty] private bool _orderBySample = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SummaryText))] private string _template = SeriesPlan.DefaultTemplate;
    private string _folder = "";

    /// <summary>Папка серии; пока её не меняли руками — следует за названием.</summary>
    public string Folder
    {
        get => _folder;
        set
        {
            if (!SetProperty(ref _folder, value ?? "")) return;
            _folderEdited = true;
        }
    }

    public ObservableCollection<SeriesMagOption> MagOptions { get; } = new();
    public ObservableCollection<SeriesKindRow> Kinds { get; } = new();

    public IReadOnlyList<FormatOption> Formats => CapturePanelViewModel.AllFormats;

    /// <summary>Формат файлов серии — общий с вкладкой «Сохранение».</summary>
    public FormatOption SelectedFormat
    {
        get => Formats.FirstOrDefault(f => f.Kind == (S.Export ??= new ExportSettings()).Format) ?? Formats[0];
        set
        {
            if (value is null || value.Kind == S.Export.Format) return;
            S.Export.Format = value.Kind;
            Host.SaveSettingsSoon();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    partial void OnNameChanged(string value)
    {
        if (!_folderEdited) SetFolderQuiet(DefaultFolder(value));
    }

    private void SetFolderQuiet(string v)
    {
        _folder = v;
        OnPropertyChanged(nameof(Folder));
    }

    private string DefaultFolder(string name)
    {
        var baseDir = (S.Export ??= new ExportSettings()).Folder;
        var safe = SeriesNaming.FileName(new SeriesPlan(name, 1, 1, new[] { 1 }, SeriesPlan.DefaultKinds, SeriesOrder.SampleBySample, "{series}"),
            default, SeriesPlan.DefaultKinds[0], "");
        return string.IsNullOrWhiteSpace(baseDir) ? safe : Path.Combine(baseDir, safe);
    }

    /// <summary>Умолчания конструктора — последние введённые (Settings.SeriesDefaults).</summary>
    private void LoadDefaults()
    {
        var d = S.SeriesDefaults ??= new SeriesDefaults();
        Groups = Math.Clamp(d.Groups, 1, 999);
        Samples = Math.Clamp(d.SamplesPerGroup, 1, 999);
        OrderBySample = d.Order == SeriesOrder.SampleBySample;
        Template = string.IsNullOrWhiteSpace(d.FileNameTemplate) ? SeriesPlan.DefaultTemplate : d.FileNameTemplate;

        foreach (var o in MagOptions) o.PropertyChanged -= OnMagOptionChanged;
        MagOptions.Clear();
        var chosen = (d.Magnifications ?? new List<int>()).Where(MagnificationSettings.IsValid).ToHashSet();
        foreach (var m in StandardMagnifications.Concat(chosen).Distinct().Order())
            AddMagOption(m, chosen.Contains(m));

        foreach (var k in Kinds) k.PropertyChanged -= OnKindChanged;
        Kinds.Clear();
        var kinds = d.Kinds is { Count: > 0 } ? d.Kinds : SeriesPlan.DefaultKinds.ToList();
        foreach (var k in kinds) AddKindRow(new SeriesKindRow(k));

        _folderEdited = false;
        Name = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        SetFolderQuiet(DefaultFolder(Name));
        _folderEdited = false;
        OnPropertyChanged(nameof(SummaryText));
    }

    private void AddMagOption(int m, bool isChecked)
    {
        var o = new SeriesMagOption(m, isChecked);
        o.PropertyChanged += OnMagOptionChanged;
        MagOptions.Add(o);
    }

    private void AddKindRow(SeriesKindRow r)
    {
        r.PropertyChanged += OnKindChanged;
        Kinds.Add(r);
    }

    private void OnMagOptionChanged(object? s, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(SummaryText));
    private void OnKindChanged(object? s, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(SummaryText));

    [RelayCommand]
    private void AddMagnification()
    {
        if (!int.TryParse(CustomMagText.Trim().TrimStart('×', 'x', 'X'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
            || !MagnificationSettings.IsValid(m))
        {
            Host.Notify("Увеличение не добавлено", "Введите целое число от 1 до 10000.", NotifyKind.Warning);
            return;
        }
        if (MagOptions.FirstOrDefault(o => o.Value == m) is { } existing) existing.IsChecked = true;
        else
        {
            var idx = MagOptions.TakeWhile(o => o.Value < m).Count();
            var o = new SeriesMagOption(m, true);
            o.PropertyChanged += OnMagOptionChanged;
            MagOptions.Insert(idx, o);
        }
        CustomMagText = "";
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    private void AddKind()
    {
        var next = Kinds.Count == 0 ? 1 : (int)Kinds.Max(k => k.Number) + 1;
        AddKindRow(new SeriesKindRow(new FrameKind(next, $"Вид {next}", FrameDecoration.ScaleBarAndMeasurements)));
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    private void RemoveKind(SeriesKindRow? row)
    {
        if (row is null || Kinds.Count <= 1) return;
        row.PropertyChanged -= OnKindChanged;
        Kinds.Remove(row);
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Папка серии" };
        var start = Directory.Exists(Folder) ? Folder : Path.GetDirectoryName(Folder);
        if (!string.IsNullOrEmpty(start) && Directory.Exists(start)) dlg.InitialDirectory = start;
        if (dlg.ShowDialog(Host.Owner) == true) Folder = dlg.FolderName;
    }

    /// <summary>План из полей конструктора.</summary>
    public SeriesPlan BuildPlan() => new(
        string.IsNullOrWhiteSpace(Name) ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Name.Trim(),
        ClampCount(Groups), ClampCount(Samples),
        MagOptions.Where(o => o.IsChecked).Select(o => o.Value).Order().ToArray(),
        Kinds.Select(k => k.ToKind()).ToArray(),
        OrderBySample ? SeriesOrder.SampleBySample : SeriesOrder.ByMagnification,
        string.IsNullOrWhiteSpace(Template) ? SeriesPlan.DefaultTemplate : Template.Trim());

    private static int ClampCount(double v) => double.IsFinite(v) ? (int)Math.Clamp(Math.Round(v), 1, 999) : 1;

    /// <summary>«N групп × M образцов × K увеличений = S шагов, F файлов» и пример имени.</summary>
    public string SummaryText
    {
        get
        {
            var p = BuildPlan();
            var ext = FrameExporter.Extension((S.Export ??= new ExportSettings()).Format);
            var sample = p.Kinds.Count > 0 && p.Magnifications.Count > 0
                ? SeriesNaming.FileName(p, new SeriesStepKey(1, 1, p.Magnifications[0]), p.Kinds[0], ext) : "—";
            return $"{RuWords.Count(p.Groups, "группа", "группы", "групп")} × {RuWords.Count(p.SamplesPerGroup, "образец", "образца", "образцов")} × " +
                   $"{RuWords.Count(p.Magnifications.Count, "увеличение", "увеличения", "увеличений")} = " +
                   $"{RuWords.Count(p.StepCount, "шаг", "шага", "шагов")}, {RuWords.Count(p.FileCount, "файл", "файла", "файлов")}\n" +
                   $"Первый файл: {sample}";
        }
    }

    [RelayCommand]
    private async Task StartSeries()
    {
        var plan = BuildPlan();
        if (plan.Magnifications.Count == 0)
        {
            Host.Notify("Серия не начата", "Отметьте хотя бы одно увеличение.", NotifyKind.Warning);
            return;
        }
        if (plan.Kinds.Count != Kinds.Count)
        {
            Host.Notify("Серия не начата", "У видов кадра повторяются номера — задайте разные.", NotifyKind.Warning);
            return;
        }
        var folder = (Folder ?? "").Trim().Trim('"');
        if (!IsValidFolder(folder))
        {
            Host.Notify("Серия не начата", $"Укажите полный путь к папке серии, например C:\\Снимки\\Серия. Введено: «{folder}»", NotifyKind.Warning);
            return;
        }
        if (File.Exists(Path.Combine(folder, SeriesStore.FileName)) &&
            !await Dialogs.ConfirmAsync(Host.Owner, "В папке уже есть серия",
                "Начать новую серию в этой папке? Состояние прежней серии будет заменено (снимки останутся). Чтобы продолжить прежнюю, выберите «Открыть серию…».",
                "Начать новую"))
            return;

        var session = new SeriesSession(plan, folder);
        try
        {
            Directory.CreateDirectory(folder);
            SeriesStore.Save(session);
        }
        catch (Exception ex)
        {
            Host.Notify("Не удалось создать серию", ex.Message, NotifyKind.Error);
            return;
        }

        var d = S.SeriesDefaults ??= new SeriesDefaults();
        d.Groups = plan.Groups;
        d.SamplesPerGroup = plan.SamplesPerGroup;
        d.Magnifications = plan.Magnifications.ToList();
        d.Kinds = plan.Kinds.ToList();
        d.Order = plan.Order;
        d.FileNameTemplate = plan.FileNameTemplate;
        Activate(session);
    }

    private static bool IsValidFolder(string path)
    {
        try { return Path.IsPathFullyQualified(path) && path.IndexOfAny(Path.GetInvalidPathChars()) < 0 && Path.GetFullPath(path).Length > 0; }
        catch (Exception) { return false; }
    }

    [RelayCommand]
    private void OpenSeries()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Папка серии (с файлом series.zscope-series.json)" };
        var start = S.LastSeriesFolder is { } l && Directory.Exists(l) ? Path.GetDirectoryName(l) : S.Export?.Folder;
        if (!string.IsNullOrEmpty(start) && Directory.Exists(start)) dlg.InitialDirectory = start;
        if (dlg.ShowDialog(Host.Owner) != true) return;
        try
        {
            Activate(SeriesStore.Load(dlg.FolderName));
        }
        catch (FileNotFoundException)
        {
            Host.Notify("Серия не открыта", "В папке нет файла серии " + SeriesStore.FileName, NotifyKind.Warning);
        }
        catch (Exception ex)
        {
            Host.Notify("Серия не открыта", ex.Message, NotifyKind.Error);
        }
    }

    /// <summary>При запуске: незавершённая серия в LastSeriesFolder → «Продолжить серию …?».</summary>
    private async Task OfferResumeAsync()
    {
        if (Runner is not null || S.LastSeriesFolder is not { Length: > 0 } folder) return;
        SeriesSession session;
        try
        {
            if (!File.Exists(Path.Combine(folder, SeriesStore.FileName))) return;
            session = SeriesStore.Load(folder);
        }
        catch (Exception) { return; }
        if (SeriesRunner.IsFinished(session)) return;

        // Дождаться, пока стартовые диалоги (драйверы) закроются: хост показывает один диалог за раз.
        await Task.Delay(1200);
        for (var i = 0; i < 300 && Wpf.Ui.Controls.ContentDialogHost.GetForWindow(Host.Owner)?.Content is ContentDialog; i++)
            await Task.Delay(300);
        if (Runner is not null) return;

        if (await Dialogs.ConfirmAsync(Host.Owner, "Незавершённая серия",
                $"Продолжить серию «{session.Plan.Name}» ({session.DoneCount} / {session.Plan.StepCount})?",
                "Продолжить", "Не сейчас", defaultIsClose: false))
            Activate(session);
    }

    private void Activate(SeriesSession session)
    {
        if (Runner is not null) Runner.Changed -= Refresh;
        PromptMagnification = null;
        Runner = new SeriesRunner(session, new Env(this));
        Runner.Changed += Refresh;
        S.LastSeriesFolder = session.Folder;
        Host.SaveSettingsSoon();
        BuildMap();
        IsActive = true;
        Runner.SyncMagnification();
        OnPropertyChanged(nameof(Runner));
        OnPropertyChanged(nameof(SeriesTitle));
        Refresh();
    }

    [RelayCommand]
    private void FinishSeries()
    {
        if (Runner is null) return;
        Runner.Changed -= Refresh;
        Runner = null;
        PromptMagnification = null;
        S.LastSeriesFolder = null;
        Host.SaveSettingsSoon();
        _items.Clear();
        Steps.Clear();
        IsActive = false;
        OnPropertyChanged(nameof(Runner));
        Refresh();
    }

    [RelayCommand]
    private void OpenSeriesFolder()
    {
        if (Runner is null) return;
        try { Process.Start(new ProcessStartInfo(Runner.Session.Folder) { UseShellExecute = true }); }
        catch (Exception ex) { Host.Notify("Не удалось открыть папку", ex.Message, NotifyKind.Error); }
    }

    // ═════ Прогон ═════

    public string SeriesTitle => Runner is { } r ? $"Серия «{r.Plan.Name}»" : "";

    public string CurrentTitle => Runner is { } r
        ? $"Группа {r.Current.Group} · Образец {r.Current.Sample} · ×{r.Current.Magnification}" : "";

    public string CurrentStateText
    {
        get
        {
            if (Runner is not { } r) return "";
            var k = r.Current;
            var rec = r.Session.RecordOf(k);
            var where = r.Session.IsInPlan(k) ? "" : "вне плана · ";
            if (r.Session.Reshooting) return where + "пересъёмка: файлы будут перезаписаны";
            return where + rec.Status switch
            {
                StepStatus.Done when r.IsPartial(k) => "уже снят частично",
                StepStatus.Done => "уже снят — снимок перезапишет его (с вопросом)",
                StepStatus.Skipped => "был пропущен",
                _ => "не снят",
            };
        }
    }

    public string ProgressText => Runner is { } r
        ? $"{r.Session.DoneCount} / {r.Plan.StepCount} {RuWords.Plural(r.Plan.StepCount, "шага", "шагов", "шагов")}"
          + (r.PartialCount > 0 ? $", из них {r.PartialCount} без масштаба" : "")
          + (r.Session.ExtraSteps.Count > 0 ? $" · вне плана {r.Session.ExtraSteps.Count}" : "")
          + (SeriesRunner.IsFinished(r.Session) ? " · серия пройдена" : "")
        : "";

    public double ProgressFraction => Runner is { Plan.StepCount: > 0 } r ? (double)r.Session.DoneCount / r.Plan.StepCount : 0;

    /// <summary>Увеличение, на которое программа сама переключилась: просим переключить микроскоп. null — не нужно.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PromptText), nameof(HasPrompt))] private int? _promptMagnification;

    public bool HasPrompt => PromptMagnification is not null;

    public string PromptText => PromptMagnification is { } m
        ? $"Переключите микроскоп на ×{m}, наведитесь и нажмите «Снять шаг»" : "";

    /// <summary>Нет калибровки для увеличения шага, а есть вид с масштабом.</summary>
    public bool NeedsCalibration => Runner is { } r && !r.CanCaptureFull(r.Current);

    public string CalibrationHint => Runner is { } r && NeedsCalibration
        ? $"×{r.Current.Magnification} не откалиброван — откалибруйте во вкладке «Масштаб» или снимите только виды без масштаба" : "";

    public bool HasCleanKinds => Runner?.Plan.Kinds.Any(k => k.Decoration == FrameDecoration.Clean) == true;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CaptureStepCommand), nameof(CaptureCleanCommand))] private bool _isCapturing;

    private bool CanCaptureStep() => Runner is not null && !IsCapturing && !NeedsCalibration;
    private bool CanCaptureClean() => Runner is not null && !IsCapturing && HasCleanKinds;

    /// <summary>«Снять шаг» и Пробел: все виды кадра текущего шага.</summary>
    [RelayCommand(CanExecute = nameof(CanCaptureStep))]
    private Task CaptureStep() => CaptureCoreAsync(cleanOnly: false);

    /// <summary>«Снять без масштаба»: только чистые виды, шаг будет «снят частично».</summary>
    [RelayCommand(CanExecute = nameof(CanCaptureClean))]
    private Task CaptureClean() => CaptureCoreAsync(cleanOnly: true);

    private async Task CaptureCoreAsync(bool cleanOnly)
    {
        if (Runner is not { } r) return;
        var frame = Host.LatestProcessedFrame;
        if (frame is null && Host.CurrentMagnification == r.Current.Magnification)
        {
            Host.Notify("Снять шаг", "Нет кадра для сохранения", NotifyKind.Warning);
            return;
        }

        IsCapturing = true;
        try
        {
            var step = r.Current;
            var prompt = PromptMagnification;
            PromptMagnification = null;
            // Оверлей и метаданные снимаются вместе с кадром, до первого await: что на экране сейчас, то и в файлах.
            var overlayNow = Host.ScalePanel.BuildOverlayState();
            var metaNow = frame is null ? null : BuildMetadata(frame);
            var res = await r.CaptureAsync((kind, path, overwrite) =>
                SaveKindAsync(frame!, overlayNow, metaNow!, r.Plan, step, kind, path, overwrite), cleanOnly);
            switch (res)
            {
                case StepCaptureResult.Captured:
                    var files = r.Session.RecordOf(step).Files.Select(Path.GetFileName);
                    Host.Notify("Шаг снят", string.Join(", ", files), NotifyKind.Success);
                    break;
                case StepCaptureResult.NeedsCalibration:
                    PromptMagnification = prompt;
                    Host.Notify("Шаг не снят", CalibrationHint, NotifyKind.Warning);
                    break;
                case StepCaptureResult.Cancelled:
                    PromptMagnification = prompt;
                    break;
            }
        }
        catch (Exception ex)
        {
            Host.Notify("Шаг не снят", ex is SeriesFileBusyException ? ex.Message : ex.Message + " Файлы шага не изменены — можно повторить.", NotifyKind.Error);
        }
        finally
        {
            IsCapturing = false;
            Refresh();
        }
    }

    /// <summary>Один вид кадра: оверлей по оформлению, метаданные с полями серии, точный путь.</summary>
    private Task<string> SaveKindAsync(CameraFrame frame, OverlayState all, CaptureMetadata meta, SeriesPlan plan, SeriesStepKey step,
        FrameKind kind, string path, bool overwrite)
    {
        // Сетка, перекрестие и проводимая линия — только для экрана.
        var overlay = kind.Decoration switch
        {
            FrameDecoration.ScaleBar => all with
            {
                ScaleBarEnabled = true, Measurements = Array.Empty<Measurement>(), PendingLine = null, Grid = false, Crosshair = false,
            },
            FrameDecoration.ScaleBarAndMeasurements => all with { ScaleBarEnabled = true, PendingLine = null, Grid = false, Crosshair = false },
            _ => null,
        };
        var extra = new Dictionary<string, string>
        {
            ["Series"] = plan.Name,
            ["Group"] = step.Group.ToString(CultureInfo.InvariantCulture),
            ["Sample"] = step.Sample.ToString(CultureInfo.InvariantCulture),
            ["Magnification"] = step.Magnification.ToString(CultureInfo.InvariantCulture),
            ["Kind"] = kind.Number.ToString(CultureInfo.InvariantCulture),
            ["KindName"] = kind.Name,
            ["Decoration"] = kind.Decoration.ToString(),
        };
        return _exporter.SaveExactAsync(frame, overlay is { IsEmpty: false } ? overlay : null, meta,
            S.Export ??= new ExportSettings(), path, overwrite, extra);
    }

    private CaptureMetadata BuildMetadata(CameraFrame frame)
    {
        var info = Host.Camera?.Info;
        var time = frame.Timestamp == default || Host.IsFrozen ? DateTime.Now : frame.Timestamp;
        return new CaptureMetadata(time, info?.Model, info?.Serial, info?.IsSimulated ?? false,
            Host.CameraPanel.CurrentExposure, Host.CameraPanel.CurrentGain, Host.Adjustments,
            "×" + Host.CurrentMagnification.ToString(CultureInfo.InvariantCulture), OverlayState.CurrentMicronsPerPixel(S));
    }

    // ───── Гибкость: у текущего шага (без параметра) или у шага карты ─────

    private SeriesStepKey? Target(SeriesStepItem? item) => item?.Key ?? Runner?.Current;

    /// <summary>Серия идёт и шаг сейчас не снимается: текущий шаг можно менять.</summary>
    private bool Idle => Runner is not null && !IsCapturing;

    private bool IsDialogOpen => Wpf.Ui.Controls.ContentDialogHost.GetForWindow(Host.Owner)?.Content is ContentDialog;

    [RelayCommand]
    private void Reshoot(SeriesStepItem? item)
    {
        if (!Idle || Target(item) is not { } k) return;
        Runner!.Reshoot(k);
    }

    /// <summary>Ctrl+Z: переснять предыдущий снятый шаг.</summary>
    [RelayCommand]
    private void ReshootPrevious()
    {
        if (!Idle) return;
        if (Runner!.ReshootPrevious()) Host.Notify("Пересъёмка", CurrentTitle + " — следующий «Снять шаг» перезапишет файлы", NotifyKind.Info);
    }

    [RelayCommand]
    private async Task DeleteStep(SeriesStepItem? item)
    {
        if (!Idle || Runner is not { } r || Target(item) is not { } k) return;
        var files = r.Session.RecordOf(k).Files;
        if (files.Count == 0)
        {
            Host.Notify("Удаление", "У шага нет снимков.", NotifyKind.Info);
            return;
        }
        var names = string.Join(", ", files.Select(Path.GetFileName));
        if (!await Dialogs.ConfirmAsync(Host.Owner, "Удалить снимки шага?",
                $"Группа {k.Group} · Образец {k.Sample} · ×{k.Magnification}: {names} (и .json). Шаг снова станет «не снят».", "Удалить"))
            return;
        if (!ReferenceEquals(Runner, r) || IsCapturing) return;
        r.Delete(k);
    }

    [RelayCommand]
    private void Skip(SeriesStepItem? item)
    {
        if (!Idle || Target(item) is not { } k) return;
        Runner!.Skip(k);
    }

    [RelayCommand]
    private void MakeCurrent(SeriesStepItem? item)
    {
        if (!Idle || item is null) return;
        Runner!.MakeCurrent(item.Key);
    }

    [RelayCommand] private void PrevGroup() { if (Idle) Runner!.Move(-1, 0); }
    [RelayCommand] private void NextGroup() { if (Idle) Runner!.Move(1, 0); }
    [RelayCommand] private void PrevSample() { if (Idle) Runner!.Move(0, -1); }
    [RelayCommand] private void NextSample() { if (Idle) Runner!.Move(0, 1); }

    /// <summary>«Изменить номер…»: произвольные группа, образец, увеличение для текущего шага.</summary>
    [RelayCommand]
    private async Task Renumber()
    {
        if (!Idle || Runner is not { } r) return;
        var c = r.Current;
        TextBox Box(int v) => new() { Text = v.ToString(CultureInfo.InvariantCulture), MinWidth = 90, ClearButtonEnabled = false };
        var g = Box(c.Group); var s = Box(c.Sample); var m = Box(c.Magnification);
        var grid = new Grid();
        for (var i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        void Cell(int col, string label, TextBox box)
        {
            var sp = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 8, 0, 0, 0) };
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = label, Opacity = 0.75, Margin = new Thickness(0, 0, 0, 6) });
            sp.Children.Add(box);
            Grid.SetColumn(sp, col);
            grid.Children.Add(sp);
        }
        Cell(0, "Группа", g); Cell(1, "Образец", s); Cell(2, "Увеличение, ×", m);
        var panel = new StackPanel();
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Файлы назовутся по этим номерам. Есть такой шаг в плане — он отметится снятым, нет — добавится «вне плана».",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Margin = new Thickness(0, 0, 0, 12),
        });
        panel.Children.Add(grid);
        var dialog = new ContentDialog
        {
            Title = "Номер шага", Content = panel, PrimaryButtonText = "Сделать текущим", CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Loaded += (_, _) => { g.Focus(); g.SelectAll(); };
        if (await Dialogs.ShowAsync(Host.Owner, dialog) != ContentDialogResult.Primary || !ReferenceEquals(Runner, r) || IsCapturing) return;

        static int? Parse(string t, int max) =>
            int.TryParse(t.Trim().TrimStart('×', 'x', 'X'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 1 && v <= max ? v : null;
        if (Parse(g.Text, 99999) is not { } gv || Parse(s.Text, 99999) is not { } sv || Parse(m.Text, MagnificationSettings.MaxMagnification) is not { } mv)
        {
            Host.Notify("Номер не изменён", "Группа и образец — целые от 1, увеличение — от 1 до 10000.", NotifyKind.Warning);
            return;
        }
        r.MakeCurrent(new SeriesStepKey(gv, sv, mv));
    }

    // ═════ Карта шагов ═════

    /// <summary>Шаги для карты: группы → образцы → увеличения, затем шаги вне плана.</summary>
    public ObservableCollection<SeriesStepItem> Steps { get; } = new();

    private void BuildMap()
    {
        _items.Clear();
        Steps.Clear();
        if (Runner is not { } r) return;
        var list = new List<SeriesStepItem>();
        var lastGroup = -1;
        foreach (var k in r.Plan.EnumerateSteps().OrderBy(k => k.Group).ThenBy(k => k.Sample).ThenBy(k => r.Plan.Magnifications.ToList().IndexOf(k.Magnification)))
        {
            var item = new SeriesStepItem(k, true, k.Group != lastGroup ? $"Группа {k.Group}" : null);
            lastGroup = k.Group;
            _items[k] = item;
            list.Add(item);
        }
        foreach (var item in list) Steps.Add(item);
        AddExtras(r);
    }

    private void AddExtras(SeriesRunner r)
    {
        var first = true;
        foreach (var k in r.Session.ExtraSteps)
        {
            if (_items.ContainsKey(k)) { first = false; continue; }
            var item = new SeriesStepItem(k, false, first && !Steps.Any(i => !i.InPlan) ? "Вне плана" : null);
            first = false;
            _items[k] = item;
            Steps.Add(item);
        }
    }

    /// <summary>Перечитать статусы шагов, текущий шаг и все подписи.</summary>
    private void Refresh()
    {
        if (Runner is { } r)
        {
            // Шаги вне плана: удалённые — убрать, новые — добавить.
            foreach (var gone in Steps.Where(i => !i.InPlan && !r.Session.ExtraSteps.Contains(i.Key)).ToList())
            {
                Steps.Remove(gone);
                _items.Remove(gone.Key);
            }
            AddExtras(r);
            foreach (var item in Steps)
            {
                var rec = r.Session.RecordOf(item.Key);
                item.Status = rec.Status;
                item.IsPartial = r.IsPartial(item.Key);
                item.IsCurrent = item.Key == r.Current;
                item.SetThumbSource(rec.Status == StepStatus.Done && rec.Files.Count > 0 ? rec.Files[0] : null, rec.When);
            }
            if (!_items.ContainsKey(r.Current)) CurrentItem = null;
            else CurrentItem = _items[r.Current];
        }
        OnPropertyChanged(nameof(SeriesTitle));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentStateText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(NeedsCalibration));
        OnPropertyChanged(nameof(CalibrationHint));
        OnPropertyChanged(nameof(HasCleanKinds));
        CaptureStepCommand.NotifyCanExecuteChanged();
        CaptureCleanCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Текущий шаг на карте (для прокрутки к нему); null — номер вне карты.</summary>
    [ObservableProperty] private SeriesStepItem? _currentItem;

    // ═════ Окружение прогона ═════

    private sealed class Env : ISeriesEnvironment
    {
        private readonly SeriesPanelViewModel _vm;
        public Env(SeriesPanelViewModel vm) => _vm = vm;

        public int CurrentMagnification
        {
            get => _vm.Host.CurrentMagnification;
            set
            {
                _vm.Host.CurrentMagnification = value;
                _vm.PromptMagnification = value;
            }
        }

        public bool IsCalibrated(int magnification) =>
            _vm.S.Magnifications?.FirstOrDefault(p => p.Magnification == magnification)?.MicronsPerPixel is > 0;

        public string FileExtension => FrameExporter.Extension((_vm.S.Export ??= new ExportSettings()).Format);
        public bool FileExists(string path) => File.Exists(path);

        public void DeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { _vm.Host.Notify("Не удалось удалить файл", ex.Message, NotifyKind.Error); }
        }

        public void MoveFile(string from, string to)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Move(from, to, overwrite: true);
        }

        public void DeleteEmptyFolder(string path)
        {
            try { if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path); }
            catch (IOException) { /* служебная папка останется — не беда */ }
            catch (UnauthorizedAccessException) { }
        }

        public Task<bool> ConfirmOverwriteAsync(IReadOnlyList<string> existing) =>
            Dialogs.ConfirmAsync(_vm.Host.Owner, "Перезаписать?",
                "Файлы уже есть: " + string.Join(", ", existing.Select(Path.GetFileName)) + ". Перезаписать их?", "Перезаписать");

        public void Persist(SeriesSession session)
        {
            try { SeriesStore.Save(session); }
            catch (Exception ex) { _vm.Host.Notify("Не удалось записать состояние серии", ex.Message, NotifyKind.Error); }
        }
    }

    /// <summary>Закрытие окна: дописать снимки шага (до 10 с) и освободить экспорт.</summary>
    private void OnOwnerClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel || _exporterDisposed) return;
        _exporterDisposed = true;
        _exporter.WaitIdle(TimeSpan.FromSeconds(10));
        _exporter.Dispose();
    }
}
