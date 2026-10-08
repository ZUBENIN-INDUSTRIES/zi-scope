using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Imaging;
using LumeneraViewer.Settings;
using LumeneraViewer.UI.Effects;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Окно «Глубокие настройки цвета» (истории 23–25). Держит текущий <see cref="DeepColor"/>,
/// каждое изменение даёт новую запись и уходит в <c>host.Adjustments</c> (не чаще ~30 раз в секунду);
/// в профиль увеличения её сохраняет хост. Внешние изменения (увеличение, профиль, сброс) перечитываются.
/// </summary>
public partial class DeepColorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan PushInterval = TimeSpan.FromMilliseconds(33);

    private static readonly string[] SectorNames =
        ["Красный", "Оранжевый", "Жёлтый", "Зелёный", "Голубой", "Синий", "Фиолетовый", "Пурпурный"];

    private readonly DispatcherTimer _pushTimer;
    private DeepColor _deep;
    private DeepColor _lastPushed;
    /// <summary>Увеличение, к которому относятся показанные значения.</summary>
    private int _mag;
    private bool _dirty;
    private bool _loading;

    public MainViewModel Host { get; }

    public DeepColorViewModel(MainViewModel host)
    {
        Host = host;
        HslRows = new ObservableCollection<HslRowViewModel>(
            SectorNames.Select((name, i) => new HslRowViewModel(this, i, name, Palette.HslSectors[i])));
        _pushTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PushInterval };
        _pushTimer.Tick += (_, _) =>
        {
            if (_dirty) Push();
            else _pushTimer.Stop();
        };
        _deep = _lastPushed = host.Adjustments.Deep ?? DeepColor.Neutral;
        _mag = host.CurrentMagnification;
        Load(_deep);

        host.PropertyChanged += OnHostPropertyChanged;
        host.CameraPanel.PropertyChanged += OnCameraPanelPropertyChanged;
        host.MagnificationChanged += OnMagnificationChanged;
        host.SettingsReplaced += ReloadFromHost;
    }

    public void Dispose()
    {
        Flush();
        _pushTimer.Stop();
        Host.PropertyChanged -= OnHostPropertyChanged;
        Host.CameraPanel.PropertyChanged -= OnCameraPanelPropertyChanged;
        Host.MagnificationChanged -= OnMagnificationChanged;
        Host.SettingsReplaced -= ReloadFromHost;
    }

    /// <summary>Гистограмма обработанного кадра (считает вкладка «Камера»).</summary>
    public Histogram? Histogram => Host.CameraPanel.Histogram;

    public int CurrentMagnification => Host.CurrentMagnification;

    // ───── RGB: усиление 0.5–2, уровни: чёрное 0–100, белое 155–255, гамма 0.2–3 ─────
    [ObservableProperty] private double _redGain = 1;
    [ObservableProperty] private double _greenGain = 1;
    [ObservableProperty] private double _blueGain = 1;
    /// <summary>Канал уровней: 0 — общий, 1 — R, 2 — G, 3 — B.</summary>
    [ObservableProperty] private int _levelsChannel;
    [ObservableProperty] private double _levelBlack;
    [ObservableProperty] private double _levelWhite = 255;
    [ObservableProperty] private double _levelGamma = 1;

    // ───── Тон ─────
    [ObservableProperty] private double _exposureEv;
    [ObservableProperty] private double _highlights;
    [ObservableProperty] private double _shadows;
    [ObservableProperty] private double _whites;
    [ObservableProperty] private double _blacks;

    // ───── Тональная кривая ─────
    /// <summary>Канал кривой: 0 — общий, 1 — R, 2 — G, 3 — B.</summary>
    [ObservableProperty] private int _curveChannel;
    [ObservableProperty] private ToneCurve _curve = ToneCurve.Identity;

    // ───── Присутствие ─────
    [ObservableProperty] private double _clarity;
    [ObservableProperty] private double _vibrance;
    [ObservableProperty] private double _dehaze;

    // ───── HSL ─────
    /// <summary>0 — оттенок, 1 — насыщенность, 2 — светлота.</summary>
    [ObservableProperty] private int _hslMode;
    public ObservableCollection<HslRowViewModel> HslRows { get; }

    // ───── Детали ─────
    [ObservableProperty] private double _sharpenAmount;
    [ObservableProperty] private double _sharpenRadius = 1;

    [RelayCommand] private void SetLevelsChannel(string ch) => LevelsChannel = int.Parse(ch);
    [RelayCommand] private void SetCurveChannel(string ch) => CurveChannel = int.Parse(ch);
    [RelayCommand] private void SetHslMode(string mode) => HslMode = int.Parse(mode);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading) return;
        var d = _deep;
        switch (e.PropertyName)
        {
            case nameof(RedGain): d = d with { RedGain = Math.Round(RedGain, 2) }; break;
            case nameof(GreenGain): d = d with { GreenGain = Math.Round(GreenGain, 2) }; break;
            case nameof(BlueGain): d = d with { BlueGain = Math.Round(BlueGain, 2) }; break;
            case nameof(LevelBlack) or nameof(LevelWhite) or nameof(LevelGamma):
                d = WithLevels(d, LevelsChannel, new ChannelLevels(Math.Round(LevelBlack), Math.Round(LevelWhite), Math.Round(LevelGamma, 2)));
                break;
            case nameof(ExposureEv): d = d with { ExposureEv = Math.Round(ExposureEv, 2) }; break;
            case nameof(Highlights): d = d with { Highlights = Math.Round(Highlights) }; break;
            case nameof(Shadows): d = d with { Shadows = Math.Round(Shadows) }; break;
            case nameof(Whites): d = d with { Whites = Math.Round(Whites) }; break;
            case nameof(Blacks): d = d with { Blacks = Math.Round(Blacks) }; break;
            case nameof(Curve): d = WithCurve(d, CurveChannel, Curve ?? ToneCurve.Identity); break;
            case nameof(Clarity): d = d with { Clarity = Math.Round(Clarity) }; break;
            case nameof(Vibrance): d = d with { Vibrance = Math.Round(Vibrance) }; break;
            case nameof(Dehaze): d = d with { Dehaze = Math.Round(Dehaze) }; break;
            case nameof(SharpenAmount): d = d with { SharpenAmount = Math.Round(SharpenAmount) }; break;
            case nameof(SharpenRadius): d = d with { SharpenRadius = Math.Round(SharpenRadius, 1) }; break;
            case nameof(LevelsChannel) or nameof(CurveChannel) or nameof(HslMode):
                Load(_deep);   // показать значения выбранного канала/режима
                return;
            default: return;
        }
        Change(d);
    }

    /// <summary>Строка HSL поменяла значение в текущем режиме.</summary>
    internal void OnHslChanged(int index, double value)
    {
        if (_loading) return;
        var h = _deep.Hsl[index];
        value = Math.Round(value);
        h = HslMode switch
        {
            0 => h with { Hue = value },
            1 => h with { Saturation = value },
            _ => h with { Luminance = value },
        };
        Change(_deep.WithHsl(index, h));
    }

    [RelayCommand]
    private void ResetSection(string section)
    {
        var n = DeepColor.Neutral;
        var d = section switch
        {
            "rgb" => _deep with
            {
                RedGain = n.RedGain, GreenGain = n.GreenGain, BlueGain = n.BlueGain,
                Master = n.Master, Red = n.Red, Green = n.Green, Blue = n.Blue,
            },
            "tone" => _deep with { ExposureEv = 0, Highlights = 0, Shadows = 0, Whites = 0, Blacks = 0 },
            "curve" => _deep with { CurveMaster = n.CurveMaster, CurveRed = n.CurveRed, CurveGreen = n.CurveGreen, CurveBlue = n.CurveBlue },
            "presence" => _deep with { Clarity = 0, Vibrance = 0, Dehaze = 0 },
            "hsl" => _deep with { Hsl = n.Hsl },
            "details" => _deep with { SharpenAmount = n.SharpenAmount, SharpenRadius = n.SharpenRadius },
            _ => _deep,
        };
        Change(d);
        Load(_deep);
    }

    [RelayCommand]
    private void ResetAll()
    {
        Change(DeepColor.Neutral);
        Load(_deep);
    }

    private void Change(DeepColor d)
    {
        if (d == _deep) return;
        _deep = d;
        _dirty = true;
        // Первое изменение — сразу, дальше не чаще раза в PushInterval
        if (!_pushTimer.IsEnabled)
        {
            Push();
            _pushTimer.Start();
        }
    }

    private void Push()
    {
        _dirty = false;
        _lastPushed = _deep;
        var a = Host.Adjustments;
        if (a.Deep != _deep) Host.Adjustments = a with { Deep = _deep };
    }

    /// <summary>Отдать хосту изменение, ещё ждущее троттлинга (перед закрытием окна или программы).</summary>
    public void Flush()
    {
        if (_dirty) Push();
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentMagnification)) OnPropertyChanged(nameof(CurrentMagnification));
        if (e.PropertyName != nameof(MainViewModel.Adjustments)) return;
        // Смена увеличения приходит сюда раньше MagnificationChanged (хост применяет живые значения)
        if (Host.CurrentMagnification != _mag) { OnMagnificationSwitched(); return; }
        // Своё изменение возвращается тем же значением; другое — внешнее (профиль, сброс)
        var deep = Host.Adjustments.Deep ?? DeepColor.Neutral;
        if (deep != _lastPushed) ReloadFromHost();
    }

    private void OnCameraPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CameraPanelViewModel.Histogram)) OnPropertyChanged(nameof(Histogram));
    }

    private void OnMagnificationChanged(int mag)
    {
        OnPropertyChanged(nameof(CurrentMagnification));
        if (mag != _mag) OnMagnificationSwitched();
    }

    /// <summary>
    /// Увеличение сменилось (один раз на смену, от первого пришедшего события). Хост уже сохранил живые
    /// значения в профиль прежнего увеличения — несброшенное изменение дописываем туда же, потом перечитываем.
    /// </summary>
    private void OnMagnificationSwitched()
    {
        if (_dirty && MagnificationSettings.IsValid(_mag))
        {
            var old = MagnificationSettings.GetOrCreate(Host.Settings, _mag);
            old.Adjustments = (old.Adjustments ?? ImageAdjustments.Default) with { Deep = _deep };
            Host.SaveSettingsSoon();
        }
        ReloadFromHost();
    }

    /// <summary>Отбросить несохранённое и показать то, что сейчас у хоста.</summary>
    private void ReloadFromHost()
    {
        _dirty = false;
        _pushTimer.Stop();
        _mag = Host.CurrentMagnification;
        _deep = _lastPushed = Host.Adjustments.Deep ?? DeepColor.Neutral;
        Load(_deep);
    }

    private void Load(DeepColor d)
    {
        _loading = true;
        try
        {
            RedGain = d.RedGain;
            GreenGain = d.GreenGain;
            BlueGain = d.BlueGain;
            var lv = LevelsOf(d, LevelsChannel);
            LevelBlack = lv.Black;
            LevelWhite = lv.White;
            LevelGamma = lv.Gamma;
            ExposureEv = d.ExposureEv;
            Highlights = d.Highlights;
            Shadows = d.Shadows;
            Whites = d.Whites;
            Blacks = d.Blacks;
            Curve = CurveOf(d, CurveChannel);
            Clarity = d.Clarity;
            Vibrance = d.Vibrance;
            Dehaze = d.Dehaze;
            SharpenAmount = d.SharpenAmount;
            SharpenRadius = d.SharpenRadius;
            for (int i = 0; i < HslRows.Count; i++)
            {
                var h = d.Hsl[i];
                HslRows[i].SetSilently(HslMode switch { 0 => h.Hue, 1 => h.Saturation, _ => h.Luminance });
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private static ChannelLevels LevelsOf(DeepColor d, int ch) => (ch switch
    {
        1 => d.Red, 2 => d.Green, 3 => d.Blue, _ => d.Master,
    }) ?? ChannelLevels.Neutral;

    private static DeepColor WithLevels(DeepColor d, int ch, ChannelLevels l) => ch switch
    {
        1 => d with { Red = l }, 2 => d with { Green = l }, 3 => d with { Blue = l }, _ => d with { Master = l },
    };

    private static ToneCurve CurveOf(DeepColor d, int ch) => (ch switch
    {
        1 => d.CurveRed, 2 => d.CurveGreen, 3 => d.CurveBlue, _ => d.CurveMaster,
    }) ?? ToneCurve.Identity;

    private static DeepColor WithCurve(DeepColor d, int ch, ToneCurve c) => ch switch
    {
        1 => d with { CurveRed = c }, 2 => d with { CurveGreen = c }, 3 => d with { CurveBlue = c }, _ => d with { CurveMaster = c },
    };
}

/// <summary>Строка HSL: цвет сектора и значение выбранного режима (−100…100).</summary>
public partial class HslRowViewModel : ObservableObject
{
    private readonly DeepColorViewModel _owner;
    private bool _silent;

    public HslRowViewModel(DeepColorViewModel owner, int index, string name, Color color)
    {
        _owner = owner;
        Index = index;
        Name = name;
        Brush = Palette.Frozen(color);
        TrackBrush = Palette.Frozen(Color.FromArgb(0x70, color.R, color.G, color.B));
    }

    public int Index { get; }
    public string Name { get; }
    public Brush Brush { get; }
    public Brush TrackBrush { get; }

    [ObservableProperty] private double _value;

    partial void OnValueChanged(double value)
    {
        if (!_silent) _owner.OnHslChanged(Index, value);
    }

    internal void SetSilently(double v)
    {
        _silent = true;
        try { Value = v; }
        finally { _silent = false; }
    }
}
