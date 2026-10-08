using System.IO;
using LumeneraViewer.Series;

namespace LumeneraViewer.Tests;

public sealed class SeriesStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "zs-series-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static readonly SeriesStepKey A = new(1, 1, 100), B = new(1, 1, 200), C = new(1, 2, 100);

    private SeriesSession New()
    {
        var kinds = new[] { new FrameKind(1, "Без масштаба", FrameDecoration.Clean), new FrameKind(5, "Измерения", FrameDecoration.ScaleBarAndMeasurements) };
        return new SeriesSession(new SeriesPlan("Опыт 8 окт", 1, 2, new[] { 100, 200 }, kinds, SeriesOrder.ByMagnification, "{series}_{group}-{kind}"), _dir);
    }

    private string[] Touch(params string[] names)
    {
        Directory.CreateDirectory(_dir);
        return names.Select(n => { var p = Path.Combine(_dir, n); File.WriteAllText(p, "x"); return p; }).ToArray();
    }

    [Fact]
    public void RoundTrip_PlanStatusesFilesCurrentReshootAndExtra()
    {
        var s = New();
        var extra = new SeriesStepKey(9, 9, 2000);
        s.MarkDone(A, Touch("a1.png", "a2.png"));
        s.MarkSkipped(C);
        s.SetCurrent(extra);
        s.MarkDone(extra, Touch("e.png"));
        s.BeginReshoot(A);
        SeriesStore.Save(s);

        Assert.True(File.Exists(Path.Combine(_dir, SeriesStore.FileName)));
        Assert.False(File.Exists(Path.Combine(_dir, SeriesStore.FileName + ".tmp")));

        var l = SeriesStore.Load(_dir);
        Assert.Equal("Опыт 8 окт", l.Plan.Name);
        Assert.Equal(1, l.Plan.Groups);
        Assert.Equal(2, l.Plan.SamplesPerGroup);
        Assert.Equal(new[] { 100, 200 }, l.Plan.Magnifications);
        Assert.Equal(s.Plan.Kinds, l.Plan.Kinds);
        Assert.Equal(SeriesOrder.ByMagnification, l.Plan.Order);
        Assert.Equal("{series}_{group}-{kind}", l.Plan.FileNameTemplate);
        Assert.Equal(_dir, l.Folder);

        Assert.Equal(StepStatus.Done, l.RecordOf(A).Status);
        Assert.Equal(s.RecordOf(A).Files, l.RecordOf(A).Files);
        Assert.Equal(s.RecordOf(A).When, l.RecordOf(A).When);
        Assert.Equal(StepStatus.Skipped, l.RecordOf(C).Status);
        Assert.Equal(StepStatus.Pending, l.RecordOf(B).Status);
        Assert.Equal(new[] { extra }, l.ExtraSteps);
        Assert.Equal(StepStatus.Done, l.RecordOf(extra).Status);
        Assert.Equal(A, l.Current);
        Assert.True(l.Reshooting);
    }

    [Fact]
    public void Load_DoneStepWithMissingFiles_BecomesPending()
    {
        var s = New();
        var files = Touch("a1.png", "a2.png");
        s.MarkDone(A, files);
        s.MarkDone(B, Touch("b1.png"));
        SeriesStore.Save(s);
        File.Delete(files[1]);

        var l = SeriesStore.Load(_dir);
        Assert.Equal(StepStatus.Pending, l.RecordOf(A).Status);
        Assert.Empty(l.RecordOf(A).Files);
        Assert.Equal(StepStatus.Done, l.RecordOf(B).Status);
        Assert.Equal(1, l.DoneCount);
    }

    [Fact]
    public void Load_BrokenFile_InvalidData()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, SeriesStore.FileName), "{ oops");
        Assert.Throws<InvalidDataException>(() => SeriesStore.Load(_dir));
    }

    [Fact]
    public void Save_OverwritesPrevious()
    {
        var s = New();
        SeriesStore.Save(s);
        s.MarkDone(A, Touch("a.png"));
        SeriesStore.Save(s);
        Assert.Equal(StepStatus.Done, SeriesStore.Load(_dir).RecordOf(A).Status);
    }

    [Fact]
    public void Load_NoFolderOrNoFile_FileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => SeriesStore.Load(Path.Combine(_dir, "нет такой")));
        Directory.CreateDirectory(_dir);
        Assert.Throws<FileNotFoundException>(() => SeriesStore.Load(_dir));
    }

    [Fact]
    public void Load_ForeignFormat_InvalidData()
    {
        var s = New();
        SeriesStore.Save(s);
        var path = Path.Combine(_dir, SeriesStore.FileName);
        File.WriteAllText(path, File.ReadAllText(path).Replace("zi-scope-series/1", "other-app/1"));
        Assert.Throws<InvalidDataException>(() => SeriesStore.Load(_dir));
        File.WriteAllText(path, "{ \"Plan\": null }");
        Assert.Throws<InvalidDataException>(() => SeriesStore.Load(_dir));
    }

    [Fact]
    public void MovedFolder_DoneStepsKept_PathsInNewFolder()
    {
        var s = New();
        s.MarkDone(A, Touch("a1.png", "a2.png"));
        SeriesStore.Save(s);
        var moved = _dir + "-moved";
        Directory.Move(_dir, moved);
        try
        {
            var l = SeriesStore.Load(moved);
            Assert.Equal(StepStatus.Done, l.RecordOf(A).Status);
            Assert.Equal(new[] { Path.Combine(moved, "a1.png"), Path.Combine(moved, "a2.png") }, l.RecordOf(A).Files);
        }
        finally { Directory.Delete(moved, true); }
    }
}
