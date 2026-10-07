using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;
using Wpf.Ui.Controls;

using LumeneraViewer.UI;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Карточка «Масштаб»: объективы, калибровка, вид плашки. Владеет событием Changed —
/// по нему превью перерисовывает оверлей. BuildOverlayState() — текущее состояние для экспорта.
/// </summary>
public partial class ScalePanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }
    private AppSettings S => Host.Settings;
    private ScaleBarSettings Bar => S.ScaleBar ??= new ScaleBarSettings();

    /// <summary>Что-то из рисуемого поменялось (UI-поток). Подписан PreviewOverlay.</summary>
    public event Action? Changed;

    public ObservableCollection<string> ObjectiveNames { get; } = new();

    public string[] Corners { get; } = { "Справа снизу", "Слева снизу", "Справа сверху", "Слева сверху" };
    public string[] ColorNames { get; } = { "Белый", "Чёрный", "Жёлтый" };
    public string[] UnitNames { get; } = { "мкм", "мм" };

    public ScalePanelViewModel(MainViewModel host)
    {
        Host = host;
        EnsureObjectives();
        ReloadNames();
        _manualLengthText = FormatManual();
        host.PropertyChanged += OnHostChanged;
        host.SettingsReplaced += OnSettingsReplaced;
    }

    /// <summary>Настройки заменены (сброс): объективы, плашка и сетка перечитываются из Settings.</summary>
    private void OnSettingsReplaced()
    {
        EnsureObjectives();
        ReloadNames();
        _manualLengthText = FormatManual();
        OnPropertyChanged(string.Empty);
        RaiseChanged(save: false);
    }

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveTool)) OnPropertyChanged(nameof(IsCalibrating));
    }

    /// <summary>Текущее состояние оверлея — для впечатывания в файл (вызывать в UI-потоке).</summary>
    public OverlayState BuildOverlayState() => OverlayState.FromSettings(S, Host.MeasurePanel?.Items.Select(i => i.Measurement));

    /// <summary>Сообщить превью и зависимым, что рисуемое поменялось, и сохранить настройки.</summary>
    public void RaiseChanged(bool save = true)
    {
        if (save) Host.SaveSettingsSoon();
        Changed?.Invoke();
    }

    // ───── Объективы ─────

    private void EnsureObjectives()
    {
        S.Objectives ??= new();
        if (S.Objectives.Count == 0) S.Objectives.Add(new ObjectiveProfile("Объектив 1", null));
        if (S.Objectives.All(o => o.Name != S.CurrentObjective)) S.CurrentObjective = S.Objectives[0].Name;
    }

    private void ReloadNames()
    {
        ObjectiveNames.Clear();
        foreach (var o in S.Objectives) ObjectiveNames.Add(o.Name);
        OnPropertyChanged(nameof(SelectedObjective));
        OnCalibrationChanged();
    }

    private ObjectiveProfile Current
    {
        get
        {
            EnsureObjectives();
            return S.Objectives.First(o => o.Name == S.CurrentObjective);
        }
    }

    public string? SelectedObjective
    {
        get => S.CurrentObjective;
        set
        {
            if (value is null || value == S.CurrentObjective || S.Objectives.All(o => o.Name != value)) return;
            S.CurrentObjective = value;
            OnPropertyChanged();
            OnCalibrationChanged();
            RaiseChanged();
        }
    }

    public double? MicronsPerPixel => OverlayState.CurrentMicronsPerPixel(S);
    public bool IsCalibrated => MicronsPerPixel is not null;
    public string CalibrationText => MicronsPerPixel is { } k
        ? k.ToString(k < 0.01 ? "0.00000" : "0.000", CultureInfo.InvariantCulture) + " мкм/px"
        : "не откалиброван — откалибруйте объектив";
    public bool CanDelete => S.Objectives.Count > 1;
    public bool IsCalibrating => Host.ActiveTool == PreviewTool.Calibrate;

    private void OnCalibrationChanged()
    {
        OnPropertyChanged(nameof(MicronsPerPixel));
        OnPropertyChanged(nameof(IsCalibrated));
        OnPropertyChanged(nameof(CalibrationText));
        OnPropertyChanged(nameof(ScaleBarEnabled));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task AddObjective()
    {
        var name = await AskTextAsync("Новый объектив", "Название (например, «10×»)", NextFreeName(), "Добавить");
        if (name is null) return;
        if (!ValidateName(name, null)) return;
        S.Objectives.Add(new ObjectiveProfile(name, null));
        S.CurrentObjective = name;
        ReloadNames();
        RaiseChanged();
        Host.Notify("Объектив добавлен", $"«{name}» ещё не откалиброван — нажмите «Калибровать».", NotifyKind.Info);
    }

    [RelayCommand]
    private async Task RenameObjective()
    {
        var cur = Current;
        var name = await AskTextAsync("Переименовать объектив", "Новое название", cur.Name, "Сохранить");
        if (name is null || name == cur.Name) return;
        if (!ValidateName(name, cur.Name)) return;
        S.Objectives[S.Objectives.IndexOf(cur)] = cur with { Name = name };
        S.CurrentObjective = name;
        ReloadNames();
        RaiseChanged();
    }

    [RelayCommand]
    private async Task DeleteObjective()
    {
        if (S.Objectives.Count <= 1)
        {
            Host.Notify("Нельзя удалить", "Должен остаться хотя бы один объектив.", NotifyKind.Warning);
            return;
        }
        var cur = Current;
        if (!await ConfirmAsync("Удалить объектив?", $"«{cur.Name}» и его калибровка будут удалены.")) return;
        var i = S.Objectives.IndexOf(cur);
        S.Objectives.RemoveAt(i);
        S.CurrentObjective = S.Objectives[Math.Min(i, S.Objectives.Count - 1)].Name;
        ReloadNames();
        RaiseChanged();
    }

    private string NextFreeName()
    {
        for (var i = S.Objectives.Count + 1; ; i++)
            if (S.Objectives.All(o => o.Name != $"Объектив {i}")) return $"Объектив {i}";
    }

    private bool ValidateName(string name, string? except)
    {
        if (S.Objectives.Any(o => o.Name == name && o.Name != except))
        {
            Host.Notify("Такое имя уже есть", "Выберите другое название объектива.", NotifyKind.Warning);
            return false;
        }
        return true;
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
            // Профиль фиксируется до диалога: пока он открыт, объектив могут сменить или удалить.
            var targetName = Current.Name;
            var text = await AskTextAsync("Известная длина, мкм",
                $"Длина линии {px.ToString("0.#", CultureInfo.InvariantCulture)} px. Сколько это микрометров?", "", "Сохранить");
            if (text is null) return;
            if (!TryParse(text, out var microns) || microns <= 0)
            {
                Host.Notify("Неверная длина", "Введите положительное число микрометров, например 100.", NotifyKind.Warning);
                return;
            }
            var k = Calibration.MicronsPerPixel(a, b, microns);
            var i = S.Objectives.FindIndex(o => o.Name == targetName);
            if (i < 0)
            {
                Host.Notify("Калибровка не сохранена", $"Объектив «{targetName}» удалён или переименован, пока шла калибровка.", NotifyKind.Warning);
                return;
            }
            S.Objectives[i] = S.Objectives[i] with { MicronsPerPixel = k };
            OnCalibrationChanged();
            RaiseChanged();
            var shown = k.ToString(k < 0.01 ? "0.00000" : "0.000", CultureInfo.InvariantCulture);
            Host.Notify("Объектив откалиброван", $"«{targetName}»: {shown} мкм/px", NotifyKind.Success);
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
        RaiseChanged();
    }

    internal static bool TryParse(string? s, out double v)
        => double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
           && !double.IsNaN(v) && !double.IsInfinity(v);

    // ───── Диалоги ─────

    private Task<string?> AskTextAsync(string title, string prompt, string initial, string ok)
        => Dialogs.AskTextAsync(Host.Owner, title, prompt, initial, ok);

    private Task<bool> ConfirmAsync(string title, string message)
        => Dialogs.ConfirmAsync(Host.Owner, title, message, "Удалить");
}
