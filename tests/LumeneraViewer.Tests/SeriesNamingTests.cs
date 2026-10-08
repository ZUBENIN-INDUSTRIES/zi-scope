using LumeneraViewer.Series;

namespace LumeneraViewer.Tests;

public sealed class SeriesNamingTests
{
    private static readonly FrameKind Clean = SeriesPlan.DefaultKinds[0];
    private static readonly FrameKind Scale = SeriesPlan.DefaultKinds[1];

    private static SeriesPlan Plan(string template = SeriesPlan.DefaultTemplate, string name = "Опыт") =>
        new(name, 2, 2, new[] { 100, 200 }, SeriesPlan.DefaultKinds, SeriesOrder.SampleBySample, template);

    [Fact]
    public void DefaultTemplate_GroupSampleMagKind()
    {
        var k = new SeriesStepKey(1, 1, 100);
        Assert.Equal("1_1_100_1.png", SeriesNaming.FileName(Plan(), k, Clean, ".png"));
        Assert.Equal("1_1_100_2.png", SeriesNaming.FileName(Plan(), k, Scale, ".png"));
        Assert.Equal("3_12_1000_2.tif", SeriesNaming.FileName(Plan(), new SeriesStepKey(3, 12, 1000), Scale, "tif"));
    }

    [Fact]
    public void SeriesToken_InsertsSeriesName()
    {
        Assert.Equal("Опыт-2_1_200_1.jpg",
            SeriesNaming.FileName(Plan("{series}-{group}_{sample}_{mag}_{kind}"), new SeriesStepKey(2, 1, 200), Clean, ".jpg"));
    }

    [Fact]
    public void InvalidCharacters_ReplacedWithUnderscore()
    {
        Assert.Equal("a_b_c_1_1_100_1.png",
            SeriesNaming.FileName(Plan("{series}_{group}_{sample}_{mag}_{kind}", "a/b:c"), new SeriesStepKey(1, 1, 100), Clean, ".png"));
        Assert.Equal("x_1_1.png",
            SeriesNaming.FileName(Plan("x?{group}|{kind}"), new SeriesStepKey(1, 1, 100), Clean, ".png"));
    }

    [Fact]
    public void EmptyTemplate_FallsBackToDefault()
    {
        Assert.Equal("1_2_100_1.png", SeriesNaming.FileName(Plan("  "), new SeriesStepKey(1, 2, 100), Clean, ".png"));
    }
}
