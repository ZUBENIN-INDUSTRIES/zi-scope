using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;

namespace LumeneraViewer.ViewModels;

/// <summary>Строка списка измерений.</summary>
public sealed partial class MeasurementItem : ObservableObject
{
    public MeasurementItem(Measurement m, int number) { Measurement = m; Number = number; }
    public Measurement Measurement { get; }
    public int Number { get; }
    [ObservableProperty] private string _label = "";
}

/// <summary>Карточка «Измерения»: линейка, список отрезков, перекрестие, сетка.</summary>
public partial class MeasurePanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }
    private GuideSettings G => Host.Settings.Guides ??= new GuideSettings();
    private int _counter;

    /// <summary>Измерения текущей сессии (в координатах кадра). В настройки не сохраняются.</summary>
    public ObservableCollection<MeasurementItem> Items { get; } = new();

    public MeasurePanelViewModel(MainViewModel host)
    {
        Host = host;
        host.PropertyChanged += OnHostChanged;
        host.ScalePanel.Changed += OnScaleChanged;
        // Измерения сняты при калибровке старого увеличения — на новом их микроны были бы неверны.
        host.MagnificationChanged += _ => ClearAll();
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasItems));
    }

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveTool)) OnPropertyChanged(nameof(IsRulerActive));
    }

    private bool? _lastCalibrated;
    private void OnScaleChanged()
    {
        // Объектив сменили/откалибровали — пересчитать подписи и единицы шага сетки.
        var k = Host.ScalePanel.MicronsPerPixel;
        foreach (var i in Items) i.Label = MeasurementText.Label(i.Measurement, k);
        if (_lastCalibrated != (k is not null))
        {
            _lastCalibrated = k is not null;
            OnPropertyChanged(nameof(GridStepText));
            OnPropertyChanged(nameof(GridStepUnit));
        }
    }

    private void Changed(bool save = true) => Host.ScalePanel.RaiseChanged(save);

    public bool HasItems => Items.Count > 0;

    public bool IsRulerActive
    {
        get => Host.ActiveTool == PreviewTool.Ruler;
        set
        {
            if (value) Host.ActiveTool = PreviewTool.Ruler;
            else if (Host.ActiveTool == PreviewTool.Ruler) Host.ActiveTool = PreviewTool.None;
        }
    }

    /// <summary>Вызывает PreviewOverlay после второго клика в режиме «Линейка».</summary>
    public void Add(Measurement m)
    {
        if (m.LengthPixels < 1) return;
        var item = new MeasurementItem(m, ++_counter) { Label = MeasurementText.Label(m, Host.ScalePanel.MicronsPerPixel) };
        Items.Add(item);
        Changed(save: false);
    }

    [RelayCommand]
    private void Remove(MeasurementItem? item)
    {
        if (item is null || !Items.Remove(item)) return;
        Changed(save: false);
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (Items.Count == 0) return;
        Items.Clear();
        _counter = 0;
        Changed(save: false);
    }

    public bool Crosshair
    {
        get => G.Crosshair;
        set { if (G.Crosshair == value) return; G.Crosshair = value; OnPropertyChanged(); Changed(); }
    }

    public bool Grid
    {
        get => G.Grid;
        set { if (G.Grid == value) return; G.Grid = value; OnPropertyChanged(); Changed(); }
    }

    public string GridStepUnit => Host.ScalePanel.IsCalibrated ? "мкм" : "% ширины";

    /// <summary>Шаг сетки: в мкм при калибровке, в % ширины без неё.</summary>
    public string GridStepText
    {
        get => (Host.ScalePanel.IsCalibrated ? G.GridStepMicrons : G.GridStepPercent).ToString("0.###", CultureInfo.InvariantCulture);
        set
        {
            if (!ScalePanelViewModel.TryParse(value, out var v) || v <= 0) return;
            if (Host.ScalePanel.IsCalibrated) G.GridStepMicrons = v;
            else G.GridStepPercent = Math.Clamp(v, 1, 100);
            Changed();
        }
    }
}
