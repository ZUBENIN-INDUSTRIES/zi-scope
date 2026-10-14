using System.Collections.ObjectModel;
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

/// <summary>Увеличение в настройках вида кадра: снимать ли этот вид на нём.</summary>
public partial class KindMagChoice : ObservableObject
{
    public KindMagChoice(int value, bool isChecked)
    {
        Value = value;
        _isChecked = isChecked;
    }

    public int Value { get; }
    public string Text => "×" + Value.ToString(CultureInfo.InvariantCulture);
    [ObservableProperty] private bool _isChecked;
}

/// <summary>Строка списка видов кадра в конструкторе.</summary>
public partial class SeriesKindRow : ObservableObject
{
    private readonly HashSet<int>? _initialMags;

    public SeriesKindRow(FrameKind k)
    {
        _number = k.Number;
        _name = k.Name;
        _decoration = SeriesKindRow.Decorations.First(d => d.Value == k.Decoration);
        _separate = k.Separate;
        _hint = k.Hint ?? "";
        _initialMags = k.Magnifications is { Count: > 0 } m ? m.ToHashSet() : null;
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

    /// <summary>Дополнительный кадр: отдельное нажатие после обычных видов шага.</summary>
    [ObservableProperty] private bool _separate;

    /// <summary>Подсказка перед дополнительным кадром.</summary>
    [ObservableProperty] private string _hint;

    /// <summary>На каких из отмеченных в серии увеличений снимать этот вид.</summary>
    public ObservableCollection<KindMagChoice> MagChoices { get; } = new();

    /// <summary>Привести список увеличений вида к увеличениям серии: новые — отмечены (или как было в сохранённом виде).</summary>
    public void SyncMagnifications(IReadOnlyList<int> seriesMags)
    {
        foreach (var gone in MagChoices.Where(c => !seriesMags.Contains(c.Value)).ToList())
        {
            gone.PropertyChanged -= OnChoiceChanged;
            MagChoices.Remove(gone);
        }
        for (var i = 0; i < seriesMags.Count; i++)
        {
            var m = seriesMags[i];
            if (MagChoices.Any(c => c.Value == m)) continue;
            var c = new KindMagChoice(m, _initialMags is null || _initialMags.Contains(m));
            c.PropertyChanged += OnChoiceChanged;
            MagChoices.Insert(Math.Min(i, MagChoices.Count), c);
        }
        OnPropertyChanged(nameof(MagSummary));
    }

    private void OnChoiceChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(MagSummary));
        OnPropertyChanged(nameof(MagChoices));
    }

    /// <summary>«на всех увеличениях» / «только ×100, ×200» / «ни на одном».</summary>
    public string MagSummary
    {
        get
        {
            var on = MagChoices.Where(c => c.IsChecked).ToList();
            if (on.Count == MagChoices.Count) return "на всех увеличениях серии";
            if (on.Count == 0) return "ни на одном увеличении — вид не будет сниматься";
            return "только " + string.Join(", ", on.Select(c => c.Text));
        }
    }

    public FrameKind ToKind()
    {
        var on = MagChoices.Where(c => c.IsChecked).Select(c => c.Value).ToArray();
        // Все увеличения отмечены — null («на всех», в том числе на добавленных позже). Ни одного — пустой фильтр
        // означал бы «на всех», поэтому несуществующее увеличение 0.
        IReadOnlyList<int>? mags = MagChoices.Count == 0 || on.Length == MagChoices.Count ? null : on.Length == 0 ? new[] { 0 } : on;
        return new FrameKind((int)Math.Clamp(Math.Round(double.IsFinite(Number) ? Number : 1), 1, 999),
            string.IsNullOrWhiteSpace(Name) ? "Вид" : Name.Trim(), Decoration?.Value ?? FrameDecoration.Clean)
        {
            Separate = Separate,
            Magnifications = mags,
            Hint = Separate && !string.IsNullOrWhiteSpace(Hint) ? Hint.Trim() : null,
        };
    }
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
    /// <summary>Обычные виды сняты, дополнительный кадр ещё ждёт.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText), nameof(Tooltip))] private bool _awaitsExtra;
    [ObservableProperty] private bool _isCurrent;

    public string StatusText => Status switch
    {
        StepStatus.Done when IsPartial => "снят частично",
        StepStatus.Done when AwaitsExtra => "ждёт доп. кадр",
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
