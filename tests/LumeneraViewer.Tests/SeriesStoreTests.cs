using System.IO;
using System.Text.Json;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;
using LumeneraViewer.Series;

namespace LumeneraViewer.Tests;

public sealed class SeriesStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "zs-series-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
        try { Directory.Delete(_dir + "-moved", true); } catch { }
    }

    private static readonly SeriesStepKey A = new(1, 1, 100), B = new(1, 1, 200), C = new(1, 2, 100);

    private static SeriesPlan Plan(string name = "Опыт 8 окт")
    {
        var kinds = new[] { new FrameKind(1, "Без масштаба", FrameDecoration.Clean), new FrameKind(5, "Измерения", FrameDecoration.ScaleBarAndMeasurements) };
        return new SeriesPlan(name, 1, 2, new[] { 100, 200 }, kinds, SeriesOrder.ByMagnification, "{series}_{group}-{kind}");
    }

    private SeriesSession New() => new(Plan(), _dir);

    private string ProjectPath => Path.Combine(_dir, "Опыт 8 окт.zisc");

    private string[] Touch(params string[] names)
    {
        Directory.CreateDirectory(_dir);
        return names.Select(n => { var p = Path.Combine(_dir, n); File.WriteAllText(p, "x"); return p; }).ToArray();
    }

    private static readonly DateTime T = new(2026, 10, 13, 9, 30, 0);

    /// <summary>Метаданные кадра, как их вернул бы экспорт: File — имя файла, Extra — поля серии.</summary>
    private static FrameMetadata Meta(string path, int kind, bool burned = false) => new(
        T, Path.GetFileName(path), ImageFormatKind.Png, 4008, 2672, "INFINITY4-11", "SN-7", false, 120.5f, 1.5f,
        new ImageAdjustments(Brightness: 7), "×100", 0.42, burned, null,
        new Dictionary<string, string> { ["Group"] = "1", ["Sample"] = "1", ["Magnification"] = "100", ["Kind"] = kind.ToString() });

    private static FrameMetadata[] Metas(string[] files) => files.Select((f, i) => Meta(f, i + 1, burned: i > 0)).ToArray();

    private JsonElement ReadJson(string? path = null)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path ?? ProjectPath));
        return doc.RootElement.Clone();
    }

    [Theory]
    [InlineData("Опыт 8 окт", "Опыт 8 окт.zisc")]
    [InlineData("a/b:c*d?", "a_b_c_d_.zisc")]
    [InlineData("", "серия.zisc")]
    [InlineData("   ", "серия.zisc")]
    public void ProjectFileName_FromSeriesName(string name, string expected) =>
        Assert.Equal(expected, SeriesStore.ProjectFileName(Plan(name)));

    [Fact]
    public void Session_ProjectFile_DefaultsToSeriesNameInFolder()
    {
        Assert.Equal(ProjectPath, New().ProjectFile);
        Assert.Equal(@"D:\x\other.zisc", new SeriesSession(Plan(), _dir, @"D:\x\other.zisc").ProjectFile);
    }

    [Fact]
    public void Save_WritesZiscWithFormat2_NoOldStateFile()
    {
        SeriesStore.Save(New());

        Assert.Equal(new[] { "Опыт 8 окт.zisc" }, Directory.GetFiles(_dir).Select(Path.GetFileName));
        Assert.Equal("zi-scope-series/2", ReadJson().GetProperty("format").GetString());
    }

    [Fact]
    public void RoundTrip_PlanStatusesFilesCurrentReshootExtraAndFrames()
    {
        var s = New();
        var extra = new SeriesStepKey(9, 9, 2000);
        var a = Touch("a1.png", "a2.png");
        s.MarkDone(A, a, Metas(a));
        s.MarkSkipped(C);
        s.SetCurrent(extra);
        var e = Touch("e.png");
        s.MarkDone(extra, e, Metas(e));
        s.BeginReshoot(A);
        SeriesStore.Save(s);

        Assert.False(File.Exists(ProjectPath + ".tmp"));
        var l = SeriesStore.Load(ProjectPath);
        Assert.Equal("Опыт 8 окт", l.Plan.Name);
        Assert.Equal(1, l.Plan.Groups);
        Assert.Equal(2, l.Plan.SamplesPerGroup);
        Assert.Equal(new[] { 100, 200 }, l.Plan.Magnifications);
        Assert.Equal(s.Plan.Kinds, l.Plan.Kinds);
        Assert.Equal(SeriesOrder.ByMagnification, l.Plan.Order);
        Assert.Equal("{series}_{group}-{kind}", l.Plan.FileNameTemplate);
        Assert.Equal(_dir, l.Folder);
        Assert.Equal(ProjectPath, l.ProjectFile);

        Assert.Equal(StepStatus.Done, l.RecordOf(A).Status);
        Assert.Equal(s.RecordOf(A).Files, l.RecordOf(A).Files);
        Assert.Equal(s.RecordOf(A).When, l.RecordOf(A).When);
        Assert.Equal(StepStatus.Skipped, l.RecordOf(C).Status);
        Assert.Equal(StepStatus.Pending, l.RecordOf(B).Status);
        Assert.Equal(new[] { extra }, l.ExtraSteps);
        Assert.Equal(StepStatus.Done, l.RecordOf(extra).Status);
        Assert.Equal(A, l.Current);
        Assert.True(l.Reshooting);

        var fa = l.RecordOf(A).Frames!;
        Assert.Equal(new[] { "a1.png", "a2.png" }, fa.Select(f => f.File));
        Assert.Equal(new[] { false, true }, fa.Select(f => f.OverlaysBurned));
        Assert.Equal((T, 4008, 2672, "SN-7", 120.5f, 1.5f, 0.42), (fa[0].Time, fa[0].Width, fa[0].Height, fa[0].CameraSerial, fa[0].ExposureMs, fa[0].Gain, fa[0].MicronsPerPixel));
        Assert.Equal(7, fa[0].Adjustments.Brightness);
        Assert.Equal("2", fa[1].Extra!["Kind"]);
        Assert.Equal(new[] { "e.png" }, l.RecordOf(extra).Frames!.Select(f => f.File));
    }

    [Fact]
    public void Save_FramesOneListOfAllDoneFiles_RelativeNames_StepsWithoutMetadata()
    {
        var s = New();
        var a = Touch("a1.png", "a2.png");
        var b = Touch("b1.png");
        s.MarkDone(A, a, Metas(a));
        s.MarkDone(B, b, Metas(b));
        s.MarkSkipped(C);
        SeriesStore.Save(s);

        var root = ReadJson();
        var frames = root.GetProperty("Frames").EnumerateArray().ToArray();
        Assert.Equal(new[] { "a1.png", "a2.png", "b1.png" }, frames.Select(f => f.GetProperty("File").GetString()).Order(StringComparer.Ordinal));
        Assert.All(frames, f => Assert.Equal("100", f.GetProperty("Extra").GetProperty("Magnification").GetString()));
        Assert.Equal(4008, frames[0].GetProperty("Width").GetInt32());
        Assert.All(root.GetProperty("Steps").EnumerateArray(), st => Assert.False(st.TryGetProperty("Frames", out _)));
        Assert.DoesNotContain(_dir, File.ReadAllText(ProjectPath).Replace(@"\\", @"\"));   // пути — без папки
    }

    [Fact]
    public void Reshoot_ReplacesStepFrames_Reset_RemovesThem()
    {
        var s = New();
        var a = Touch("a1.png", "a2.png");
        s.MarkDone(A, a, Metas(a));
        var b = Touch("b1.png");
        s.MarkDone(B, b, Metas(b));
        s.BeginReshoot(A);
        var again = Touch("a1.png");
        s.MarkDone(A, again, new[] { Meta(again[0], 1) with { Time = T.AddHours(1) } });
        s.Reset(B);
        SeriesStore.Save(s);

        var frames = ReadJson().GetProperty("Frames").EnumerateArray().ToArray();
        Assert.Single(frames);
        Assert.Equal("a1.png", frames[0].GetProperty("File").GetString());
        Assert.Equal(T.AddHours(1), frames[0].GetProperty("Time").GetDateTime());
    }

    [Fact]
    public void Load_DoneStepWithMissingFiles_BecomesPending_FramesDropped()
    {
        var s = New();
        var files = Touch("a1.png", "a2.png");
        s.MarkDone(A, files, Metas(files));
        var b = Touch("b1.png");
        s.MarkDone(B, b, Metas(b));
        SeriesStore.Save(s);
        File.Delete(files[1]);

        var l = SeriesStore.Load(ProjectPath);
        Assert.Equal(StepStatus.Pending, l.RecordOf(A).Status);
        Assert.Empty(l.RecordOf(A).Files);
        Assert.Equal(StepStatus.Done, l.RecordOf(B).Status);
        Assert.Equal(1, l.DoneCount);

        SeriesStore.Save(l);
        Assert.Equal(new[] { "b1.png" }, ReadJson().GetProperty("Frames").EnumerateArray().Select(f => f.GetProperty("File").GetString()));
    }

    [Fact]
    public void LoadSave_HandEditedProject_FrameStaysUnderItsOwnFile()
    {
        var s = New();
        var a = Touch("a1.png", "a2.png");
        s.MarkDone(A, a, Metas(a));
        SeriesStore.Save(s);

        // Проект поправили вручную: у шага из двух файлов осталась одна запись — для второго файла.
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ProjectPath))!;
        var list = json["Frames"]!.AsArray();
        list.Remove(list.Single(f => f!["File"]!.GetValue<string>() == "a1.png"));
        File.WriteAllText(ProjectPath, json.ToJsonString());

        SeriesStore.Save(SeriesStore.Load(ProjectPath));

        var frames = ReadJson().GetProperty("Frames").EnumerateArray().ToArray();
        Assert.Single(frames);
        Assert.Equal("a2.png", frames[0].GetProperty("File").GetString());
        Assert.Equal("2", frames[0].GetProperty("Extra").GetProperty("Kind").GetString());
        Assert.True(frames[0].GetProperty("OverlaysBurned").GetBoolean());
    }

    [Fact]
    public void Load_BrokenFile_InvalidData()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ProjectPath, "{ oops");
        Assert.Throws<InvalidDataException>(() => SeriesStore.Load(ProjectPath));
    }

    [Fact]
    public void Save_OverwritesPrevious()
    {
        var s = New();
        SeriesStore.Save(s);
        s.MarkDone(A, Touch("a.png"));
        SeriesStore.Save(s);
        Assert.Equal(StepStatus.Done, SeriesStore.Load(ProjectPath).RecordOf(A).Status);
    }

    [Fact]
    public void Load_NoFolderOrNoFile_FileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => SeriesStore.Load(Path.Combine(_dir, "нет такой", "x.zisc")));
        Directory.CreateDirectory(_dir);
        Assert.Throws<FileNotFoundException>(() => SeriesStore.Load(ProjectPath));
    }

    [Theory]
    [InlineData("other-app/1")]
    [InlineData("zi-scope-series/1")]
    [InlineData("zi-scope-series/3")]
    public void Load_ForeignOrOldFormat_InvalidData(string format)
    {
        SeriesStore.Save(New());
        File.WriteAllText(ProjectPath, File.ReadAllText(ProjectPath).Replace("zi-scope-series/2", format));
        Assert.Throws<InvalidDataException>(() => SeriesStore.Load(ProjectPath));
    }

    [Fact]
    public void Load_NoPlan_InvalidData()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ProjectPath, "{ \"format\": \"zi-scope-series/2\", \"Plan\": null }");
        Assert.Throws<InvalidDataException>(() => SeriesStore.Load(ProjectPath));
    }

    [Fact]
    public void MovedFolder_DoneStepsKept_PathsInNewFolder()
    {
        var s = New();
        var a = Touch("a1.png", "a2.png");
        s.MarkDone(A, a, Metas(a));
        SeriesStore.Save(s);
        var moved = _dir + "-moved";
        Directory.Move(_dir, moved);

        var l = SeriesStore.Load(Path.Combine(moved, "Опыт 8 окт.zisc"));
        Assert.Equal(moved, l.Folder);
        Assert.Equal(StepStatus.Done, l.RecordOf(A).Status);
        Assert.Equal(new[] { Path.Combine(moved, "a1.png"), Path.Combine(moved, "a2.png") }, l.RecordOf(A).Files);
        Assert.Equal(new[] { "a1.png", "a2.png" }, l.RecordOf(A).Frames!.Select(f => f.File));
    }

    [Fact]
    public void RenamedProject_SavedToSameFile()
    {
        SeriesStore.Save(New());
        var renamed = Path.Combine(_dir, "мой проект.zisc");
        File.Move(ProjectPath, renamed);

        var l = SeriesStore.Load(renamed);
        Assert.Equal(renamed, l.ProjectFile);
        l.MarkSkipped(A);
        SeriesStore.Save(l);

        Assert.Equal(new[] { "мой проект.zisc" }, Directory.GetFiles(_dir).Select(Path.GetFileName));
        Assert.Equal(StepStatus.Skipped, SeriesStore.Load(renamed).RecordOf(A).Status);
    }

    [Fact]
    public void FindProject_NoFolderOrNoProject_Null()
    {
        Assert.Null(SeriesStore.FindProject(Path.Combine(_dir, "нет такой")));
        Touch("a.png", "series.zscope-series.json");   // старый файл состояния не считается проектом
        Assert.Null(SeriesStore.FindProject(_dir));
    }

    [Fact]
    public void FindProject_SeveralProjects_NewestByWriteTime()
    {
        var older = Touch("первая.zisc", "вторая.zisc", "третья.zisc");
        File.SetLastWriteTimeUtc(older[0], new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(older[1], new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(older[2], new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(older[1], SeriesStore.FindProject(_dir));
    }
}
