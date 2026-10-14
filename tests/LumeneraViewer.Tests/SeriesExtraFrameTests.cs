using System.IO;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;
using LumeneraViewer.Series;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Tests;

/// <summary>Дополнительный кадр шага: отдельное нажатие после обычных видов, увеличения вида, пропуск, проект .zisc.</summary>
public sealed class SeriesExtraFrameTests : IDisposable
{
    private static readonly SeriesStepKey A = new(1, 1, 100), B = new(1, 1, 200), C = new(1, 2, 100);

    // 1, 2 — обычные; 3 — доп. кадр с масштабом только на ×100
    private static readonly FrameKind[] Kinds =
    {
        new(1, "Без масштаба", FrameDecoration.Clean),
        new(2, "С масштабом", FrameDecoration.ScaleBar),
        new(3, "Другое поле", FrameDecoration.ScaleBar) { Separate = true, Magnifications = new[] { 100 }, Hint = "Сдвиньте препарат" },
    };

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "zs-extra-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>Программа на настоящей временной папке: файлы пишутся, проект сохраняется в .zisc.</summary>
    private sealed class DiskEnv : ISeriesEnvironment
    {
        public int CurrentMagnification { get; set; } = 100;
        public bool IsCalibrated(int m) => true;
        public string FileExtension => ".png";
        public bool FileExists(string path) => File.Exists(path);
        public void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
        public void MoveFile(string from, string to) { Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Move(from, to, true); }
        public void DeleteEmptyFolder(string path)
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
        }
        public int Questions;
        public Task<bool> ConfirmOverwriteAsync(IReadOnlyList<string> existing) { Questions++; return Task.FromResult(true); }
        public void Persist(SeriesSession session) => SeriesStore.Save(session);
    }

    private static Task<FrameMetadata> Save(FrameKind k, string path, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3]);
        return Task.FromResult(new FrameMetadata(DateTime.Now, path, ImageFormatKind.Png, 64, 48, "Simulated", "SIM", true, 10f, 1f,
            ImageAdjustments.Default, "×100", 0.5, k.Decoration != FrameDecoration.Clean, null,
            new Dictionary<string, string> { ["Kind"] = k.Number.ToString() }));
    }

    private (SeriesRunner R, DiskEnv Env) New(int samples = 2)
    {
        var plan = new SeriesPlan("Опыт", 1, samples, new[] { 100, 200 }, Kinds, SeriesOrder.SampleBySample);
        var env = new DiskEnv();
        var session = new SeriesSession(plan, _dir);
        SeriesStore.Save(session);
        return (new SeriesRunner(session, env), env);
    }

    private string P(string name) => Path.Combine(_dir, name);

    private string[] Images => Directory.Exists(_dir)
        ? Directory.GetFiles(_dir, "*.png").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!
        : Array.Empty<string>();

    [Fact]
    public void Plan_KindsAtMagnification_AndFileCount()
    {
        var plan = new SeriesPlan("Опыт", 1, 2, new[] { 100, 200 }, Kinds, SeriesOrder.SampleBySample);
        Assert.Equal(new[] { 1, 2, 3 }, plan.KindsAt(100).Select(k => k.Number));
        Assert.Equal(new[] { 1, 2 }, plan.KindsAt(200).Select(k => k.Number));
        Assert.Equal(new[] { 1, 2 }, plan.MainKindsAt(100).Select(k => k.Number));
        Assert.Equal(new[] { 3 }, plan.SeparateKindsAt(100).Select(k => k.Number));
        Assert.Equal(2 * (3 + 2), plan.FileCount);
    }

    [Fact]
    public async Task FirstPress_MainKinds_StepStaysCurrent_SecondPress_ExtraFrame3_ThenNextStep()
    {
        var (r, _) = New();

        await r.CaptureAsync(Save);
        Assert.Equal(new[] { "1_1_100_1.png", "1_1_100_2.png" }, Images);
        Assert.Equal(A, r.Current);                                 // ждёт доп. кадр
        Assert.Equal(3, r.NextSeparateKind(A)!.Number);
        Assert.False(r.IsPartial(A));
        Assert.False(SeriesRunner.IsFinished(r.Session));

        await r.CaptureAsync(Save);
        Assert.Equal(new[] { "1_1_100_1.png", "1_1_100_2.png", "1_1_100_3.png" }, Images);
        Assert.Equal(B, r.Current);
        Assert.Null(r.NextSeparateKind(A));
        Assert.Equal(new[] { "1_1_100_1.png", "1_1_100_2.png", "1_1_100_3.png" }, r.Session.RecordOf(A).Frames!.Select(f => f.File));
    }

    [Fact]
    public async Task MagnificationWithoutExtraKind_OnePressPerStep()
    {
        var (r, env) = New();
        await r.CaptureAsync(Save);
        await r.CaptureAsync(Save);                                 // доп. кадр ×100
        Assert.Equal(B, r.Current);
        env.CurrentMagnification = 200;

        await r.CaptureAsync(Save);
        Assert.Contains("1_1_200_2.png", Images);
        Assert.DoesNotContain("1_1_200_3.png", Images);
        Assert.Equal(C, r.Current);
    }

    [Fact]
    public async Task SkipExtra_StepComplete_NoFile_SkipSaved()
    {
        var (r, _) = New();
        await r.CaptureAsync(Save);

        Assert.True(r.SkipSeparate());

        Assert.Equal(B, r.Current);
        Assert.DoesNotContain("1_1_100_3.png", Images);
        Assert.Equal(new[] { 3 }, r.Session.RecordOf(A).SkippedKinds);
        Assert.False(r.SkipSeparate());                            // на B доп. кадра нет
    }

    [Fact]
    public async Task ExtraFrameState_SurvivesReopen_FromZisc()
    {
        var (r, env) = New();
        await r.CaptureAsync(Save);                                 // обычные сняты, доп. кадр ждёт

        var reopened = new SeriesRunner(SeriesStore.Load(r.Session.ProjectFile), env);
        Assert.Equal(A, reopened.Current);
        Assert.Equal(3, reopened.NextSeparateKind(A)!.Number);
        Assert.Equal(Kinds, reopened.Plan.Kinds);                    // Separate, увеличения и подсказка — в проекте

        await reopened.CaptureAsync(Save);
        Assert.Contains("1_1_100_3.png", Images);
        Assert.Equal(B, reopened.Current);

        // Пропуск тоже сохраняется
        var (r2, env2) = (reopened, env);
        r2.MakeCurrent(C);
        env2.CurrentMagnification = 100;
        await r2.CaptureAsync(Save);
        r2.SkipSeparate();
        var again = SeriesStore.Load(r2.Session.ProjectFile);
        Assert.Equal(new[] { 3 }, again.RecordOf(C).SkippedKinds);
    }

    [Fact]
    public async Task Reshoot_RetakesMainKinds_ThenAsksExtraAgain_ThenReturnsToFirstPending()
    {
        var (r, env) = New();
        await r.CaptureAsync(Save);
        await r.CaptureAsync(Save);                                 // A целиком, текущий B
        r.Reshoot(A);
        env.CurrentMagnification = 100;

        await r.CaptureAsync(Save);
        Assert.Equal(A, r.Current);                                 // после обычных — снова доп. кадр
        Assert.Equal(3, r.NextSeparateKind(A)!.Number);
        Assert.DoesNotContain("1_1_100_3.png", Images);             // прежний доп. кадр снят заново, старый убран

        await r.CaptureAsync(Save);
        Assert.Contains("1_1_100_3.png", Images);
        Assert.Equal(B, r.Current);                                 // к первому неснятому
        Assert.Equal(0, env.Questions);                             // при пересъёмке без вопросов
    }

    [Fact]
    public async Task LeftStepWithoutExtra_SeriesNotFinished_ComesBackAtTheEnd()
    {
        var (r, env) = New(samples: 1);                             // шаги A (×100) и B (×200)
        await r.CaptureAsync(Save);                                 // A: обычные
        r.MakeCurrent(B);                                           // ушёл, не сняв доп. кадр
        env.CurrentMagnification = 200;

        await r.CaptureAsync(Save);                                 // B
        Assert.Equal(A, r.Current);                                 // вернулись за доп. кадром
        Assert.Equal(1, r.AwaitingExtraCount);
        Assert.False(SeriesRunner.IsFinished(r.Session));

        env.CurrentMagnification = 100;
        await r.CaptureAsync(Save);
        Assert.True(SeriesRunner.IsFinished(r.Session));
    }

    [Fact]
    public async Task Delete_RemovesExtraFrameToo()
    {
        var (r, _) = New();
        await r.CaptureAsync(Save);
        await r.CaptureAsync(Save);

        r.Delete(A);

        Assert.DoesNotContain(Images, f => f.StartsWith("1_1_100_"));
        Assert.Equal(StepStatus.Pending, r.Session.RecordOf(A).Status);
    }

    [Fact]
    public void FrameKind_EqualityComparesMagnificationsByValue()
    {
        var a = Kinds[2];
        var b = new FrameKind(3, "Другое поле", FrameDecoration.ScaleBar) { Separate = true, Magnifications = new List<int> { 100 }, Hint = "Сдвиньте препарат" };
        Assert.Equal(a, b);
        Assert.NotEqual(a, b with { Magnifications = null });
        Assert.True(a.AppliesTo(100));
        Assert.False(a.AppliesTo(200));
        Assert.True(Kinds[0].AppliesTo(200));
    }
}
