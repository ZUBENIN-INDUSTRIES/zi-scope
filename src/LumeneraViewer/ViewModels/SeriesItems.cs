using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using LumeneraViewer.Series;

namespace LumeneraViewer.ViewModels;

/// <summary>Увеличение в конструкторе серии: отметка «снимать на нём».</summary>
public partial class SeriesMagOption : ObservableObject
{
    public SeriesMagOption(int value, bool isChecked)
    {
        Value = value;
        _isChecked = isChecked;
    }

    public int Value { get; }
    public string Text => "×" + Value.ToString(CultureInfo.InvariantCulture);
    [ObservableProperty] private bool _isChecked;
}

/// <summary>Оформление вида кадра с подписью для списка.</summary>
public sealed record DecorationOption(FrameDecoration Value, string Name);

/// <summary>Строка списка видов кадра в конструкторе.</summary>
public partial class SeriesKindRow : ObservableObject
{
    public SeriesKindRow(FrameKind k)
    {
        _number = k.Number;
        _name = k.Name;
        _decoration = SeriesKindRow.Decorations.First(d => d.Value == k.Decoration);
    }

    public static IReadOnlyList<DecorationOption> Decorations { get; } = new[]
    {
        new DecorationOption(FrameDecoration.Clean, "Чистый"),
        new DecorationOption(FrameDecoration.ScaleBar, "С плашкой масштаба"),
        new DecorationOption(FrameDecoration.ScaleBarAndMeasurements, "С плашкой и измерениями"),
    };

    public IReadOnlyList<DecorationOption> DecorationOptions => Decorations;

    [ObservableProperty] private double _number;
    [ObservableProperty] private string _name;
    [ObservableProperty] private DecorationOption _decoration;

    public FrameKind ToKind() => new((int)Math.Clamp(Math.Round(double.IsFinite(Number) ? Number : 1), 1, 999),
        string.IsNullOrWhiteSpace(Name) ? "Вид" : Name.Trim(), Decoration?.Value ?? FrameDecoration.Clean);
}

/// <summary>Шаг на карте серии: статус, текущий ли, миниатюра первого снятого вида.</summary>
public partial class SeriesStepItem : ObservableObject
{
    public SeriesStepItem(SeriesStepKey key, bool inPlan, string? header)
    {
        Key = key;
        InPlan = inPlan;
        Header = header;
    }

    public SeriesStepKey Key { get; }
    public bool InPlan { get; }
    /// <summary>Подпись над первым шагом группы («Группа 2», «Вне плана»); null — не первый.</summary>
    public string? Header { get; }
    public bool IsGroupStart => Header is not null;

    public string Title => $"{Key.Group}·{Key.Sample}";
    public string MagText => "×" + Key.Magnification.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText), nameof(Tooltip))] private StepStatus _status;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText), nameof(Tooltip))] private bool _isPartial;
    [ObservableProperty] private bool _isCurrent;

    public string StatusText => Status switch
    {
        StepStatus.Done when IsPartial => "снят частично",
        StepStatus.Done => "снят",
        StepStatus.Skipped => "пропущен",
        _ => "не снят",
    };

    public string Tooltip => $"Группа {Key.Group} · Образец {Key.Sample} · {MagText} — {StatusText}" + (InPlan ? "" : " (вне плана)");

    // ───── Миниатюра: грузится лениво, когда плитка видна (карта виртуализирована) ─────

    private string? _thumbPath;
    private DateTime? _thumbStamp;
    private ImageSource? _thumbnail;
    private bool _loading;

    /// <summary>Обновить источник миниатюры; другой файл или другое время съёмки — перечитать.</summary>
    public void SetThumbSource(string? path, DateTime? when)
    {
        if (path == _thumbPath && when == _thumbStamp) return;
        _thumbPath = path;
        _thumbStamp = when;
        _thumbnail = null;
        OnPropertyChanged(nameof(Thumbnail));
    }

    public ImageSource? Thumbnail
    {
        get
        {
            if (_thumbnail is null && !_loading && _thumbPath is { } p) Load(p, _thumbStamp);
            return _thumbnail;
        }
    }

    private void Load(string path, DateTime? stamp)
    {
        _loading = true;
        var ui = Application.Current?.Dispatcher;
        Task.Run(() =>
        {
            ImageSource? img = null;
            try
            {
                if (File.Exists(path))
                {
                    var b = new BitmapImage();
                    b.BeginInit();
                    b.UriSource = new Uri(path);
                    b.DecodePixelWidth = 96;
                    b.CacheOption = BitmapCacheOption.OnLoad;
                    b.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    b.EndInit();
                    b.Freeze();
                    img = b;
                }
            }
            catch (Exception) { /* битый или занятый файл — без миниатюры */ }
            ui?.BeginInvoke(() =>
            {
                _loading = false;
                if (path != _thumbPath || stamp != _thumbStamp) { OnPropertyChanged(nameof(Thumbnail)); return; }
                _thumbnail = img;
                if (img is not null) OnPropertyChanged(nameof(Thumbnail));
            });
        });
    }
}

/// <summary>Числительные по-русски: 1 группа, 2 группы, 5 групп.</summary>
public static class RuWords
{
    public static string Plural(int n, string one, string few, string many)
    {
        var a = Math.Abs(n) % 100;
        if (a is >= 11 and <= 14) return many;
        return (a % 10) switch { 1 => one, 2 or 3 or 4 => few, _ => many };
    }

    public static string Count(int n, string one, string few, string many) => $"{n} {Plural(n, one, few, many)}";
}
