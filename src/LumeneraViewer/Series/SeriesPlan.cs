using System.Text.Json.Serialization;

namespace LumeneraViewer.Series;

/// <summary>Порядок обхода шагов серии.</summary>
public enum SeriesOrder
{
    /// <summary>Образец за образцом: (1,1,×100) → (1,1,×200) → (1,2,×100) → …</summary>
    SampleBySample,
    /// <summary>По увеличениям: все образцы на ×100, затем все на ×200.</summary>
    ByMagnification,
}

/// <summary>Что впечатывается в кадр данного вида.</summary>
public enum FrameDecoration { Clean, ScaleBar, ScaleBarAndMeasurements }

/// <summary>Вид кадра: номер (последняя цифра имени файла), название и оформление.</summary>
public sealed record FrameKind(int Number, string Name, FrameDecoration Decoration);

/// <summary>Ключ шага серии: группа, образец в группе, увеличение.</summary>
public readonly record struct SeriesStepKey(int Group, int Sample, int Magnification);

/// <summary>План серии: что и в каком порядке снимать.</summary>
public sealed record SeriesPlan(
    string Name,
    int Groups,
    int SamplesPerGroup,
    IReadOnlyList<int> Magnifications,
    IReadOnlyList<FrameKind> Kinds,
    SeriesOrder Order,
    string FileNameTemplate = SeriesPlan.DefaultTemplate)
{
    public const string DefaultTemplate = "{group}_{sample}_{mag}_{kind}";

    /// <summary>Увеличения в порядке обхода; повторы отбрасываются (остаётся первое).</summary>
    public IReadOnlyList<int> Magnifications { get => _magnifications; init => _magnifications = DistinctMags(value); }
    private readonly IReadOnlyList<int> _magnifications = DistinctMags(Magnifications);

    /// <summary>Виды кадра; вид с уже встречавшимся номером отбрасывается (остаётся первый).</summary>
    public IReadOnlyList<FrameKind> Kinds { get => _kinds; init => _kinds = DistinctKinds(value); }
    private readonly IReadOnlyList<FrameKind> _kinds = DistinctKinds(Kinds);

    private static IReadOnlyList<int> DistinctMags(IReadOnlyList<int>? v) =>
        v is null ? Array.Empty<int>() : v.Distinct().ToArray();

    private static IReadOnlyList<FrameKind> DistinctKinds(IReadOnlyList<FrameKind>? v) =>
        v is null ? Array.Empty<FrameKind>() : v.Where(k => k is not null).DistinctBy(k => k.Number).ToArray();

    /// <summary>Списки сравниваются поэлементно.</summary>
    public bool Equals(SeriesPlan? other) =>
        other is not null && (ReferenceEquals(this, other) ||
            Name == other.Name && Groups == other.Groups && SamplesPerGroup == other.SamplesPerGroup &&
            Order == other.Order && FileNameTemplate == other.FileNameTemplate &&
            Magnifications.SequenceEqual(other.Magnifications) && Kinds.SequenceEqual(other.Kinds));

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Name); h.Add(Groups); h.Add(SamplesPerGroup); h.Add(Order); h.Add(FileNameTemplate);
        foreach (var m in Magnifications) h.Add(m);
        foreach (var k in Kinds) h.Add(k);
        return h.ToHashCode();
    }

    /// <summary>Виды кадра по умолчанию: 1 — без масштаба, 2 — с масштабом.</summary>
    public static IReadOnlyList<FrameKind> DefaultKinds { get; } = new[]
    {
        new FrameKind(1, "Без масштаба", FrameDecoration.Clean),
        new FrameKind(2, "С масштабом", FrameDecoration.ScaleBar),
    };

    /// <summary>Все шаги плана в порядке обхода.</summary>
    public IReadOnlyList<SeriesStepKey> EnumerateSteps()
    {
        var steps = new List<SeriesStepKey>(Math.Max(0, StepCount));
        if (Order == SeriesOrder.ByMagnification)
        {
            foreach (var m in Magnifications)
                for (var g = 1; g <= Groups; g++)
                    for (var s = 1; s <= SamplesPerGroup; s++)
                        steps.Add(new SeriesStepKey(g, s, m));
        }
        else
        {
            for (var g = 1; g <= Groups; g++)
                for (var s = 1; s <= SamplesPerGroup; s++)
                    foreach (var m in Magnifications)
                        steps.Add(new SeriesStepKey(g, s, m));
        }
        return steps;
    }

    [JsonIgnore]
    public int StepCount => Math.Max(0, Groups) * Math.Max(0, SamplesPerGroup) * Magnifications.Count;

    /// <summary>Файлов изображений: по одному на каждый вид кадра каждого шага.</summary>
    [JsonIgnore]
    public int FileCount => StepCount * Kinds.Count;
}
