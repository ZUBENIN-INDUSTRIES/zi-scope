using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.ImageCalibration;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;
using Wpf.Ui.Controls;

using LumeneraViewer.UI;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Вкладка «Масштаб»: калибровка и длина плашки текущего увеличения, вид плашки, перенос старых калибровок. Владеет событием Changed —
/// по нему превью перерисовывает оверлей. BuildOverlayState() — текущее состояние для экспорта.
/// </summary>
public partial class ScalePanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }
    private AppSettings S => Host.Settings;
    private ScaleBarSettings Bar => S.ScaleBar ??= new ScaleBarSettings();

    /// <summary>Что-то из рисуемого поменялось (UI-поток). Подписан PreviewOverlay.</summary>
    public event Action? Changed;

    public string[] Corners { get; } = { "Справа снизу", "Слева снизу", "Справа сверху", "Слева сверху" };
    public string[] ColorNames { get; } = { "Белый", "Чёрный", "Жёлтый" };
    public string[] UnitNames { get; } = { "мкм", "мм" };

    public ScalePanelViewModel(MainViewModel host)
    {
        Host = host;
        _manualLengthText = FormatManual();
        ReloadLegacy();
        RefreshPixelSizes();
        host.PropertyChanged += OnHostChanged;
        host.SettingsReplaced += Reload;
        if (host.AvailableMagnifications is System.Collections.Specialized.INotifyCollectionChanged mags)
            mags.CollectionChanged += (_, _) => RefreshPixelSizes();
    }

    /// <summary>
    /// Перечитать всё из Settings: после переключения увеличения (своя калибровка и длина плашки)
    /// и после замены настроек. Превью перерисовывается сразу.
    /// </summary>
    public void Reload()
    {
        _manualLengthText = FormatManual();
        ReloadLegacy();
        RefreshPixelSizes();
        OnPropertyChanged(string.Empty);
        RaiseChanged(save: false);
    }

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveTool)) OnPropertyChanged(nameof(IsCalibrating));
        if (e.PropertyName == nameof(MainViewModel.CurrentMagnification)) { OnPropertyChanged(nameof(MagnificationText)); RefreshPixelSizes(); }
    }

    /// <summary>Текущее состояние оверлея — для впечатывания в файл (вызывать в UI-потоке).</summary>
    public OverlayState BuildOverlayState() => OverlayState.FromSettings(S, Host.MeasurePanel?.Items.Select(i => i.Measurement));

    /// <summary>Сообщить превью и зависимым, что рисуемое поменялось, и сохранить настройки.</summary>
    public void RaiseChanged(bool save = true)
    {
        if (save) Host.SaveSettingsSoon();
        Changed?.Invoke();
    }

    // ───── Текущее увеличение ─────

    public string MagnificationText => "×" + S.CurrentMagnification.ToString(CultureInfo.InvariantCulture);

    public double? MicronsPerPixel => OverlayState.CurrentMicronsPerPixel(S);
    public bool IsCalibrated => MicronsPerPixel is not null;
    public string CalibrationText => MicronsPerPixel is { } k
        ? FormatMpp(k) + " мкм/px"
        : "не откалиброван — нажмите «Калибровать»";
    public bool IsCalibrating => Host.ActiveTool == PreviewTool.Calibrate;

    private static string FormatMpp(double k) => k.ToString(k < 0.01 ? "0.00000" : "0.000", CultureInfo.InvariantCulture);

    private void OnCalibrationChanged()
    {
        OnPropertyChanged(nameof(MicronsPerPixel));
        OnPropertyChanged(nameof(IsCalibrated));
        OnPropertyChanged(nameof(CalibrationText));
        OnPropertyChanged(nameof(ScaleBarEnabled));
        RefreshPixelSizes();
        Host.NotifyCalibrationChanged();
    }

    /// <summary>
    /// Записать калибровку в профиль увеличения навсегда: профиль, список в шапке, превью и немедленная запись
    /// настроек на диск (G04). Зовут калибровка по снимку и «Принять пересчёт» (UI-поток).
    /// </summary>
    public bool SaveCalibration(int magnification, double micronsPerPixel)
    {
        if (!MagnificationSettings.IsValid(magnification) || !(micronsPerPixel > 0) || double.IsInfinity(micronsPerPixel)) return false;
        MagnificationSettings.GetOrCreate(S, magnification).MicronsPerPixel = micronsPerPixel;
        Host.AddMagnification(magnification);
        ReloadLegacy();
        OnCalibrationChanged();
        RaiseChanged(save: false);
        var saved = Host.SaveSettingsNow();
        Host.Notify("Увеличение откалибровано", $"×{magnification}: {FormatMpp(micronsPerPixel)} мкм/px", NotifyKind.Success);
        return saved;
    }

    // ───── Калибровка по снимку и таблица размера пикселя (G02–G05) ─────

    [RelayCommand]
    private void CalibrateFromImage() => Views.CalibrationFromImageWindow.PickAndShow(Host);

    /// <summary>Таблица «Увеличение · мкм/px · Источник» по всем профилям.</summary>
    public ObservableCollection<PixelSizeRowItem> PixelSizes { get; } = new();

    private void RefreshPixelSizes()
    {
        var rows = PixelSizeTable.Build((S.Magnifications ?? new()).Select(m => (m.Magnification, m.MicronsPerPixel)));
        PixelSizes.Clear();
        foreach (var r in rows) PixelSizes.Add(new PixelSizeRowItem(r, r.Magnification == S.CurrentMagnification));
    }

    [RelayCommand]
    private void AcceptDerived(PixelSizeRowItem? row)
    {
        if (row is { CanAccept: true, Row.MicronsPerPixel: { } k }) SaveCalibration(row.Row.Magnification, k);
    }

    // ───── Старые калибровки объективов (история 10) ─────

    /// <summary>Не перенесённые старые калибровки; у каждой — выбор увеличения и «Перенести».</summary>
    public ObservableCollection<LegacyCalibrationItem> LegacyCalibrations { get; } = new();
    public bool HasLegacyCalibrations => LegacyCalibrations.Count > 0;

    private void ReloadLegacy()
    {
        LegacyCalibrations.Clear();
        if (!S.LegacyCalibrationDismissed)
        {
            // Перенесённой считается калибровка, чьё значение уже стоит в каком-то профиле увеличения.
            var used = (S.Magnifications ?? new()).Select(m => m.MicronsPerPixel).ToHashSet();
            foreach (var o in MagnificationSettings.LegacyCalibrations(S).Where(o => !used.Contains(o.MicronsPerPixel)))
                LegacyCalibrations.Add(new LegacyCalibrationItem(o, Host.AvailableMagnifications.ToList(), S.CurrentMagnification));
        }
        OnPropertyChanged(nameof(HasLegacyCalibrations));
        OnPropertyChanged(nameof(LegacySummary));
    }

    /// <summary>
    /// Пояснение над списком. Значения мкм/px здесь не повторяются — они в строках ниже, каждая своей строкой:
    /// в сплошном тексте WPF рвёт «мкм/px» после «/», и WORD JOINER этого не держит.
    /// </summary>
    public string LegacySummary => LegacyCalibrations.Count == 1
        ? "Найдена старая калибровка объектива. Выберите, какому увеличению она соответствует."
        : $"Найдено старых калибровок объективов: {LegacyCalibrations.Count}. Выберите, какому увеличению соответствует каждая.";

    [RelayCommand]
    private void TransferLegacy(LegacyCalibrationItem? item)
    {
        if (item is null) return;
        MagnificationSettings.GetOrCreate(S, item.TargetMagnification).MicronsPerPixel = item.MicronsPerPixel;
        Host.AddMagnification(item.TargetMagnification);   // синхронизирует список в шапке
        ReloadLegacy();
        OnCalibrationChanged();
        RaiseChanged();
        Host.Notify("Калибровка перенесена", $"{item.Name} → ×{item.TargetMagnification}: {FormatMpp(item.MicronsPerPixel)} мкм/px", NotifyKind.Success);
    }

    [RelayCommand]
    private void DismissLegacy()
    {
        S.LegacyCalibrationDismissed = true;
        ReloadLegacy();
        Host.SaveSettingsSoon();
    }

    // ───── Калибровка ─────

    [RelayCommand]
    private void Calibrate()
    {
        if (IsCalibrating) { Host.ActiveTool = PreviewTool.None; return; }
        Host.ActiveTool = PreviewTool.Calibrate;
        Host.Notify("Калибровка", "Кликните в начале и в конце известного отрезка объект-микрометра. Esc — отмена.", NotifyKind.Info);
    }

    /// <summary>Вызывает PreviewOverlay после второго клика в режиме калибровки.</summary>
    public async Task CompleteCalibrationAsync(Point a, Point b)
    {
        try
        {
            var px = (b - a).Length;
            if (px < 5)
            {
                Host.Notify("Слишком короткая линия", "Проведите линию по известному отрезку подлиннее.", NotifyKind.Warning);
                return;
            }
            // Увеличение фиксируется до диалога: пока он открыт, его могут переключить.
            var targetMag = S.CurrentMagnification;
            var text = await AskTextAsync("Известная длина, мкм",
                $"Длина линии {px.ToString("0.#", CultureInfo.InvariantCulture)} px. Сколько это микрометров?", "", "Сохранить");
            if (text is null) return;
            if (!TryParse(text, out var microns) || microns <= 0)
            {
                Host.Notify("Неверная длина", "Введите положительное число микрометров, например 100.", NotifyKind.Warning);
                return;
            }
            var k = Calibration.MicronsPerPixel(a, b, microns);
            MagnificationSettings.GetOrCreate(S, targetMag).MicronsPerPixel = k;
            Host.AddMagnification(targetMag);
            OnCalibrationChanged();
            RaiseChanged();
            Host.Notify("Увеличение откалибровано", $"×{targetMag}: {FormatMpp(k)} мкм/px", NotifyKind.Success);
        }
        finally
        {
            if (Host.ActiveTool == PreviewTool.Calibrate) Host.ActiveTool = PreviewTool.None;
        }
    }

    // ───── Плашка ─────

    /// <summary>Показ плашки. Без калибровки всегда false (масштаб не врёт).</summary>
    public bool ScaleBarEnabled
    {
        get => Bar.Enabled && IsCalibrated;
        set { if (IsCalibrated) Set(() => Bar.Enabled = value); }
    }

    public int CornerIndex
    {
        get => (int)Bar.Corner;
        set => Set(() => { Bar.Corner = (ScaleBarCorner)Math.Clamp(value, 0, 3); Bar.CustomX = Bar.CustomY = null; });
    }

    public int ColorIndex { get => (int)Bar.Color; set => Set(() => Bar.Color = (OverlayColor)Math.Clamp(value, 0, 2)); }

    /// <summary>Цвета для текста, линейки и подложки плашки.</summary>
    public IReadOnlyList<OverlayColors.Named> BarColorChoices => OverlayColors.All;

    private (System.Windows.Media.Color Text, System.Windows.Media.Color Bar, System.Windows.Media.Color Bg) EffectiveBarColors()
    {
        var c = OverlayRenderer.ScaleBarColors(OverlayState.FromSettings(Host.Settings));
        return (c.Text, c.Bar, System.Windows.Media.Color.FromRgb(c.Background.R, c.Background.G, c.Background.B));
    }

    public int TextColorIndex
    {
        get => OverlayColors.IndexOf(EffectiveBarColors().Text);
        set { if (value >= 0 && value < OverlayColors.All.Count) Set(() => Bar.TextColor = OverlayColors.All[value].Hex); }
    }
    public int BarColorIndex
    {
        get => OverlayColors.IndexOf(EffectiveBarColors().Bar);
        set { if (value >= 0 && value < OverlayColors.All.Count) Set(() => Bar.BarColor = OverlayColors.All[value].Hex); }
    }
    public int BackgroundColorIndex
    {
        get => OverlayColors.IndexOf(EffectiveBarColors().Bg);
        set { if (value >= 0 && value < OverlayColors.All.Count) Set(() => Bar.BackgroundColor = OverlayColors.All[value].Hex); }
    }
    /// <summary>Непрозрачность подложки, %.</summary>
    public double BackgroundOpacityPercent
    {
        get => Math.Round(Math.Clamp(Bar.BackgroundOpacity, 0, 1) * 100);
        set => Set(() => Bar.BackgroundOpacity = Math.Clamp(Math.Round(value) / 100.0, 0, 1));
    }
    public bool Bold { get => Bar.Bold; set => Set(() => Bar.Bold = value); }
    public bool Background { get => Bar.Background; set => Set(() => Bar.Background = value); }
    public double FontSize { get => Bar.FontSize; set => Set(() => Bar.FontSize = Math.Round(Math.Clamp(value, 10, 48))); }
    public double Thickness { get => Bar.Thickness; set => Set(() => Bar.Thickness = Math.Round(Math.Clamp(value, 2, 12))); }
    public bool HasCustomPosition => Bar.CustomX is not null;

    public int UnitIndex
    {
        get => (int)Bar.Unit;
        set
        {
            Set(() => Bar.Unit = (LengthUnit)Math.Clamp(value, 0, 1));
            ManualLengthText = FormatManual();
        }
    }

    public bool AutoLength
    {
        get => Bar.FixedMicrons is null;
        set
        {
            if (value) Set(() => Bar.FixedMicrons = null);
            else if (Bar.FixedMicrons is null)
            {
                // Стартовое значение — то, что сейчас подобрано автоматически.
                var k = MicronsPerPixel;
                var w = Host.FrameWidth > 0 ? Host.FrameWidth : 1600;
                Set(() => Bar.FixedMicrons = k is { } kk ? ScaleBarMath.Choose(kk, w).lengthMicrons : 100);
            }
            ManualLengthText = FormatManual();
        }
    }

    public bool IsManualLength => !AutoLength;

    private string _manualLengthText;
    /// <summary>Длина вручную в выбранных единицах (мкм или мм).</summary>
    public string ManualLengthText
    {
        get => _manualLengthText;
        set
        {
            if (!SetProperty(ref _manualLengthText, value)) return;
            if (TryParse(value, out var v) && v > 0)
            {
                var microns = Bar.Unit == LengthUnit.Millimeters ? v * 1000 : v;
                if (Bar.FixedMicrons != microns) Set(() => Bar.FixedMicrons = microns);
            }
        }
    }

    private string FormatManual()
    {
        if (Bar.FixedMicrons is not { } m) return "";
        var v = Bar.Unit == LengthUnit.Millimeters ? m / 1000 : m;
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private void ResetPosition() => Set(() => Bar.CustomX = Bar.CustomY = null);

    /// <summary>Перетаскивание плашки: левый верхний угол в долях кадра. save=false — во время drag.</summary>
    public void SetCustomPosition(double fx, double fy, bool save)
    {
        Bar.CustomX = Math.Clamp(fx, 0, 1);
        Bar.CustomY = Math.Clamp(fy, 0, 1);
        OnPropertyChanged(nameof(HasCustomPosition));
        RaiseChanged(save);
    }

    private void Set(Action apply, [System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
    {
        apply();
        OnPropertyChanged(prop);
        OnPropertyChanged(nameof(AutoLength));
        OnPropertyChanged(nameof(IsManualLength));
        OnPropertyChanged(nameof(HasCustomPosition));
        OnPropertyChanged(nameof(TextColorIndex));
        OnPropertyChanged(nameof(BarColorIndex));
        OnPropertyChanged(nameof(BackgroundColorIndex));
        RaiseChanged();
    }

    internal static bool TryParse(string? s, out double v)
        => double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
           && !double.IsNaN(v) && !double.IsInfinity(v);

    // ───── Диалоги ─────

    private Task<string?> AskTextAsync(string title, string prompt, string initial, string ok)
        => Dialogs.AskTextAsync(Host.Owner, title, prompt, initial, ok);
}

/// <summary>Строка таблицы размера пикселя для разметки.</summary>
public sealed class PixelSizeRowItem(PixelSizeRow row, bool isCurrent)
{
    public PixelSizeRow Row { get; } = row;
    public bool IsCurrent { get; } = isCurrent;
    public string MagnificationText => "×" + Row.Magnification.ToString(CultureInfo.InvariantCulture);
    public string ValueText => Row.MicronsPerPixel is { } k ? k.ToString(k < 0.01 ? "0.00000" : "0.0000", CultureInfo.InvariantCulture) : "—";
    public string SourceText => Row.Source switch
    {
        PixelSizeSource.Calibrated => "калибровка",
        PixelSizeSource.Derived => $"пересчёт от ×{Row.DerivedFrom}",
        _ => "нет данных",
    };
    public bool CanAccept => Row.Source == PixelSizeSource.Derived;
}

/// <summary>Старая калибровка объектива и увеличение, в которое её перенести.</summary>
public partial class LegacyCalibrationItem : ObservableObject
{
    public LegacyCalibrationItem(ObjectiveProfile o, IReadOnlyList<int> magnifications, int current)
    {
        Name = o.Name;
        MicronsPerPixel = o.MicronsPerPixel ?? 0;
        Magnifications = magnifications;
        _targetMagnification = current;
    }

    public string Name { get; }
    public double MicronsPerPixel { get; }
    public IReadOnlyList<int> Magnifications { get; }
    [ObservableProperty] private int _targetMagnification;
}
