using LumeneraViewer.Series;

namespace LumeneraViewer.Tests;

public sealed class SeriesPlanTests
{
    private static SeriesPlan Plan(SeriesOrder order) =>
        new("Серия", 2, 2, new[] { 100, 200 }, SeriesPlan.DefaultKinds, order);

    [Fact]
    public void SampleBySample_AllMagnificationsOfSampleThenNextSample()
    {
        var expected = new[]
        {
            new SeriesStepKey(1, 1, 100), new SeriesStepKey(1, 1, 200),
            new SeriesStepKey(1, 2, 100), new SeriesStepKey(1, 2, 200),
            new SeriesStepKey(2, 1, 100), new SeriesStepKey(2, 1, 200),
            new SeriesStepKey(2, 2, 100), new SeriesStepKey(2, 2, 200),
        };
        Assert.Equal(expected, Plan(SeriesOrder.SampleBySample).EnumerateSteps());
    }

    [Fact]
    public void ByMagnification_AllSamplesAt100ThenAllAt200()
    {
        var expected = new[]
        {
            new SeriesStepKey(1, 1, 100), new SeriesStepKey(1, 2, 100),
            new SeriesStepKey(2, 1, 100), new SeriesStepKey(2, 2, 100),
            new SeriesStepKey(1, 1, 200), new SeriesStepKey(1, 2, 200),
            new SeriesStepKey(2, 1, 200), new SeriesStepKey(2, 2, 200),
        };
        Assert.Equal(expected, Plan(SeriesOrder.ByMagnification).EnumerateSteps());
    }

    [Fact]
    public void Counts_StepsAndFiles()
    {
        // 16 групп × 4 образца × 3 увеличения = 192 шага, по 2 вида кадра = 384 файла
        var p = new SeriesPlan("x", 16, 4, new[] { 100, 200, 500 }, SeriesPlan.DefaultKinds, SeriesOrder.SampleBySample);
        Assert.Equal(192, p.StepCount);
        Assert.Equal(384, p.FileCount);
        Assert.Equal(192, p.EnumerateSteps().Count);
    }

    [Fact]
    public void DefaultKinds_CleanThenScaleBar()
    {
        Assert.Equal(new[]
        {
            new FrameKind(1, "Без масштаба", FrameDecoration.Clean),
            new FrameKind(2, "С масштабом", FrameDecoration.ScaleBar),
        }, SeriesPlan.DefaultKinds);
    }

    [Fact]
    public void DuplicateMagnificationsAndKindNumbers_Dropped()
    {
        var kinds = new[]
        {
            new FrameKind(1, "Без масштаба", FrameDecoration.Clean),
            new FrameKind(1, "Повтор", FrameDecoration.ScaleBar),
            new FrameKind(2, "С масштабом", FrameDecoration.ScaleBar),
        };
        var p = new SeriesPlan("x", 1, 1, new[] { 100, 200, 100 }, kinds, SeriesOrder.SampleBySample);
        Assert.Equal(new[] { 100, 200 }, p.Magnifications);
        Assert.Equal(new[] { 1, 2 }, p.Kinds.Select(k => k.Number));
        Assert.Equal("Без масштаба", p.Kinds[0].Name);
        Assert.Equal(new[] { new SeriesStepKey(1, 1, 100), new SeriesStepKey(1, 1, 200) }, p.EnumerateSteps());
        Assert.Equal(4, p.FileCount);

        var w = p with { Magnifications = new[] { 500, 500 } };
        Assert.Equal(new[] { 500 }, w.Magnifications);
    }

    [Fact]
    public void Equality_ComparesListsByValue_IncludingAfterJson()
    {
        var a = Plan(SeriesOrder.SampleBySample);
        var b = new SeriesPlan("Серия", 2, 2, new List<int> { 100, 200 }, SeriesPlan.DefaultKinds.ToList(), SeriesOrder.SampleBySample);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, b with { Magnifications = new[] { 200, 100 } });

        var json = System.Text.Json.JsonSerializer.Serialize(a);
        Assert.Equal(a, System.Text.Json.JsonSerializer.Deserialize<SeriesPlan>(json));
    }
}
