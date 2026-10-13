using System.IO;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;
using LumeneraViewer.Series;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Tests;

public sealed class SeriesRunnerTests
{
    private const string Folder = @"C:\series";
    // 1 группа × 2 образца × [100, 200], образец за образцом: (1,1,100) (1,1,200) (1,2,100) (1,2,200)
    private static readonly SeriesStepKey A = new(1, 1, 100), B = new(1, 1, 200), C = new(1, 2, 100), D = new(1, 2, 200);

    /// <summary>Программа без файлов на диске: файлы — словарь «путь → номер записи», всё в памяти.</summary>
    private sealed class FakeEnv : ISeriesEnvironment
    {
        public int CurrentMagnification { get; set; } = 100;
        public HashSet<int> Calibrated { get; } = new() { 100, 200 };
        public Dictionary<string, int> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Deleted { get; } = new();
        public List<int> MagnificationSets { get; } = new();
        public bool AnswerOverwrite { get; set; }
        public int Questions { get; private set; }
        public int Persisted { get; set; }

        public bool IsCalibrated(int m) => Calibrated.Contains(m);
        public string FileExtension => ".png";
        public bool FileExists(string path) => Files.ContainsKey(path);
        public void DeleteFile(string path) { Deleted.Add(path); Files.Remove(path); }
        /// <summary>Однократный сбой переименования (файл занят другой программой).</summary>
        public Func<string, string, bool>? FailMove { get; set; }
        public List<string> RemovedFolders { get; } = new();
        public void DeleteEmptyFolder(string path)
        {
            if (Files.Keys.Any(f => f.StartsWith(path + @"\", StringComparison.OrdinalIgnoreCase))) return;
            RemovedFolders.Add(path);
        }
        public void MoveFile(string from, string to)
        {
            if (FailMove is { } fail && fail(from, to)) { FailMove = null; throw new IOException("sharing violation"); }
            if (!Files.Remove(from, out var v)) throw new FileNotFoundException(from);
            Files[to] = v;
        }
        public Task<bool> ConfirmOverwriteAsync(IReadOnlyList<string> existing) { Questions++; return Task.FromResult(AnswerOverwrite); }
        public void Persist(SeriesSession session) => Persisted++;

        int ISeriesEnvironment.CurrentMagnification
        {
            get => CurrentMagnification;
            set { MagnificationSets.Add(value); CurrentMagnification = value; }
        }

        /// <summary>Файлы снимков в папке серии (без временных).</summary>
        public string[] Images => Files.Keys.Where(f => f.EndsWith(".png") && !f.Contains(@"\" + SeriesRunner.TempFolderName + @"\"))
            .Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Запись вида: только снимок (как экспорт серии — без .json); <see cref="FailAt"/> — номер вызова, который падает.
    /// В метаданных File — полный путь записи (временный): итоговое имя ставит runner. Extra["Gen"] — номер записи.
    /// </summary>
    private sealed class Saver
    {
        private readonly FakeEnv _env;
        private int _generation;
        public Saver(FakeEnv env) => _env = env;
        public List<(FrameKind Kind, string Path, bool Overwrite)> Calls { get; } = new();
        public int? FailAt { get; set; }

        public Task<FrameMetadata> Save(FrameKind k, string path, bool overwrite)
        {
            if (!overwrite && _env.Files.ContainsKey(path)) throw new IOException("exists");
            Calls.Add((k, path, overwrite));
            if (Calls.Count == FailAt) throw new IOException("диск полон");
            var g = ++_generation;
            _env.Files[path] = g;
            return Task.FromResult(new FrameMetadata(DateTime.Now, path, ImageFormatKind.Png, 64, 48, "Simulated", "SIM", true, 10f, 1f,
                ImageAdjustments.Default, "×100", 0.5, k.Decoration != FrameDecoration.Clean, null,
                new Dictionary<string, string> { ["Kind"] = k.Number.ToString(), ["Gen"] = g.ToString() }));
        }
    }

    private static (SeriesRunner R, FakeEnv Env, Saver Save) New(IReadOnlyList<FrameKind>? kinds = null)
    {
        var plan = new SeriesPlan("s", 1, 2, new[] { 100, 200 }, kinds ?? SeriesPlan.DefaultKinds, SeriesOrder.SampleBySample);
        var env = new FakeEnv();
        return (new SeriesRunner(new SeriesSession(plan, Folder), env), env, new Saver(env));
    }

    private static string P(string name) => Path.Combine(Folder, name);

    [Fact]
    public async Task Capture_SavesBothKindsByTemplate_MarksDone_AndMovesOn()
    {
        var (r, env, saver) = New();

        var res = await r.CaptureAsync(saver.Save);

        Assert.Equal(StepCaptureResult.Captured, res);
        Assert.Equal(new[] { P("1_1_100_1.png"), P("1_1_100_2.png") }, env.Images);
        Assert.DoesNotContain(env.Files.Keys, f => f.EndsWith(".json"));
        var frames = r.Session.RecordOf(A).Frames!;
        Assert.Equal(new[] { "1_1_100_1.png", "1_1_100_2.png" }, frames.Select(f => f.File));
        Assert.Equal(new[] { "1", "2" }, frames.Select(f => f.Extra!["Kind"]));
        Assert.Equal(new[] { FrameDecoration.Clean, FrameDecoration.ScaleBar }, saver.Calls.Select(c => c.Kind.Decoration));
        Assert.Equal(StepStatus.Done, r.Session.RecordOf(A).Status);
        Assert.Equal(new[] { P("1_1_100_1.png"), P("1_1_100_2.png") }, r.Session.RecordOf(A).Files);
        Assert.Equal(B, r.Current);
        Assert.Equal(A, r.LastCaptured);
        Assert.Equal(1, env.Persisted);
    }

    [Fact]
    public async Task Capture_NextStepHasOtherMagnification_SwitchesHost()
    {
        var (r, env, saver) = New();

        await r.CaptureAsync(saver.Save);

        Assert.Equal(200, env.CurrentMagnification);
        Assert.Equal(new[] { 200 }, env.MagnificationSets);
    }

    [Fact]
    public async Task Capture_HostOnOtherMagnification_SwitchesAndSavesNothing()
    {
        var (r, env, saver) = New();
        env.CurrentMagnification = 500;

        var res = await r.CaptureAsync(saver.Save);

        Assert.Equal(StepCaptureResult.MagnificationSwitched, res);
        Assert.Equal(100, env.CurrentMagnification);
        Assert.Empty(saver.Calls);
        Assert.Equal(A, r.Current);
    }

    [Fact]
    public async Task Capture_NoCalibration_Blocked_CleanOnlyGivesPartialStep()
    {
        var (r, env, saver) = New();
        env.Calibrated.Clear();

        Assert.False(r.CanCaptureFull(A));
        Assert.Equal(StepCaptureResult.NeedsCalibration, await r.CaptureAsync(saver.Save));
        Assert.Empty(saver.Calls);

        Assert.Equal(StepCaptureResult.Captured, await r.CaptureAsync(saver.Save, cleanOnly: true));
        Assert.Equal(new[] { P("1_1_100_1.png") }, env.Images);
        Assert.Equal(StepStatus.Done, r.Session.RecordOf(A).Status);
        Assert.True(r.IsPartial(A));
        Assert.Equal(B, r.Current);
    }

    [Fact]
    public async Task Capture_FileExists_AsksBeforeOverwrite()
    {
        var (r, env, saver) = New();
        env.Files[P("1_1_100_2.png")] = -1;

        env.AnswerOverwrite = false;
        Assert.Equal(StepCaptureResult.Cancelled, await r.CaptureAsync(saver.Save));
        Assert.Empty(saver.Calls);
        Assert.Equal(-1, env.Files[P("1_1_100_2.png")]);
        Assert.Equal(A, r.Current);

        env.AnswerOverwrite = true;
        Assert.Equal(StepCaptureResult.Captured, await r.CaptureAsync(saver.Save));
        Assert.Equal(2, env.Questions);
        Assert.NotEqual(-1, env.Files[P("1_1_100_2.png")]);
    }

    [Fact]
    public async Task Capture_SecondKindFails_NothingLeftOnDisk_StepUnchanged()
    {
        var (r, env, saver) = New();
        saver.FailAt = 2;

        await Assert.ThrowsAsync<IOException>(() => r.CaptureAsync(saver.Save));

        Assert.Empty(env.Files);
        Assert.Contains(P(SeriesRunner.TempFolderName), env.RemovedFolders);
        Assert.Equal(StepStatus.Pending, r.Session.RecordOf(A).Status);
        Assert.Equal(A, r.Current);
        Assert.Equal(0, env.Persisted);

        // Повтор не спрашивает про «свои» файлы неудачной попытки.
        saver.FailAt = null;
        Assert.Equal(StepCaptureResult.Captured, await r.CaptureAsync(saver.Save));
        Assert.Equal(0, env.Questions);
    }

    [Fact]
    public async Task Reshoot_SecondKindFails_OldSetIntact()
    {
        var (r, env, saver) = New();
        await r.CaptureAsync(saver.Save);
        var before = new Dictionary<string, int>(env.Files, StringComparer.OrdinalIgnoreCase);
        r.Reshoot(A);
        env.Persisted = 0;
        saver.FailAt = saver.Calls.Count + 2;

        await Assert.ThrowsAsync<IOException>(() => r.CaptureAsync(saver.Save));

        Assert.Equal(before.OrderBy(x => x.Key), env.Files.OrderBy(x => x.Key));
        Assert.Contains(P(SeriesRunner.TempFolderName), env.RemovedFolders);
        Assert.Equal(new[] { P("1_1_100_1.png"), P("1_1_100_2.png") }, r.Session.RecordOf(A).Files);
        Assert.Equal(A, r.Current);
        Assert.True(r.Session.Reshooting);
        Assert.Equal(0, env.Persisted);
    }

    [Fact]
    public async Task Capture_RenameOfSecondKindFails_NothingLeft_BusyMessage()
    {
        var (r, env, saver) = New();
        env.FailMove = (_, to) => to == P("1_1_100_2.png");

        var ex = await Assert.ThrowsAsync<SeriesFileBusyException>(() => r.CaptureAsync(saver.Save));

        Assert.Contains("1_1_100_2.png", ex.Message);
        Assert.Contains("занят другой программой", ex.Message);
        Assert.Empty(env.Files);
        Assert.Contains(P(SeriesRunner.TempFolderName), env.RemovedFolders);
        Assert.Equal(StepStatus.Pending, r.Session.RecordOf(A).Status);
        Assert.Equal(A, r.Current);
        Assert.Equal(0, env.Persisted);
    }

    [Theory]
    [InlineData(true)]    // старый «_2» занят — не отодвигается
    [InlineData(false)]   // новый «_2» не встаёт на место
    public async Task Reshoot_RenameOfSecondKindFails_OldSetIntact(bool oldBusy)
    {
        var (r, env, saver) = New();
        await r.CaptureAsync(saver.Save);
        var before = new Dictionary<string, int>(env.Files, StringComparer.OrdinalIgnoreCase);
        r.Reshoot(A);
        env.Persisted = 0;
        env.RemovedFolders.Clear();
        env.FailMove = oldBusy
            ? (from, _) => from == P("1_1_100_2.png")
            : (from, to) => to == P("1_1_100_2.png") && from.Contains(SeriesRunner.TempFolderName);

        await Assert.ThrowsAsync<SeriesFileBusyException>(() => r.CaptureAsync(saver.Save));

        Assert.Equal(before.OrderBy(x => x.Key), env.Files.OrderBy(x => x.Key));
        Assert.Contains(P(SeriesRunner.TempFolderName), env.RemovedFolders);
        Assert.Equal(new[] { P("1_1_100_1.png"), P("1_1_100_2.png") }, r.Session.RecordOf(A).Files);
        Assert.True(r.Session.Reshooting);
        Assert.Equal(0, env.Persisted);
    }

    [Fact]
    public async Task Reshoot_CleanOnly_RemovesOldScaleBarFile()
    {
        var (r, env, saver) = New();
        await r.CaptureAsync(saver.Save);
        r.Reshoot(A);

        await r.CaptureAsync(saver.Save, cleanOnly: true);

        Assert.Equal(new[] { P("1_1_100_1.png") }, env.Images);
        Assert.Equal(new[] { P("1_1_100_1.png") }, r.Session.RecordOf(A).Files);
        Assert.Equal(new[] { "1_1_100_1.png" }, r.Session.RecordOf(A).Frames!.Select(f => f.File));
    }

    [Fact]
    public async Task Reshoot_OverwritesWithoutQuestion_ThenReturnsToFirstPending()
    {
        var (r, env, saver) = New();
        await r.CaptureAsync(saver.Save);   // A → текущий B
        await r.CaptureAsync(saver.Save);   // B → текущий C
        var old = env.Files[P("1_1_100_2.png")];

        r.Reshoot(A);
        Assert.Equal(A, r.Current);
        Assert.Equal(100, env.CurrentMagnification);
        Assert.Equal(StepCaptureResult.Captured, await r.CaptureAsync(saver.Save));

        Assert.Equal(0, env.Questions);
        Assert.NotEqual(old, env.Files[P("1_1_100_2.png")]);
        Assert.Equal(C, r.Current);
        // Записи кадров шага заменены новыми.
        Assert.Equal(new[] { env.Files[P("1_1_100_1.png")], env.Files[P("1_1_100_2.png")] }.Select(g => g.ToString()),
            r.Session.RecordOf(A).Frames!.Select(f => f.Extra!["Gen"]));
    }

    [Fact]
    public async Task Capture_Reshoot_Delete_LeaveJsonNextToImagesAlone()
    {
        // Парных .json у снимков серии нет: чужой .json рядом runner не переносит и не удаляет.
        var (r, env, saver) = New();
        env.Files[P("1_1_100_1.json")] = -5;
        await r.CaptureAsync(saver.Save);
        r.Reshoot(A);
        await r.CaptureAsync(saver.Save);
        Assert.Equal(-5, env.Files[P("1_1_100_1.json")]);

        env.Deleted.Clear();
        r.Delete(A);

        Assert.Equal(new[] { P("1_1_100_1.png"), P("1_1_100_2.png") }, env.Deleted.Order(StringComparer.Ordinal));
        Assert.Equal(-5, env.Files[P("1_1_100_1.json")]);
    }

    [Fact]
    public async Task ReshootPrevious_MakesLastCapturedCurrent()
    {
        var (r, _, saver) = New();
        Assert.False(r.ReshootPrevious());
        await r.CaptureAsync(saver.Save);

        Assert.True(r.ReshootPrevious());
        Assert.Equal(A, r.Current);
        Assert.True(r.Session.Reshooting);
    }

    [Fact]
    public async Task Delete_RemovesImages_StepPendingAgain_FramesGone()
    {
        var (r, env, saver) = New();
        await r.CaptureAsync(saver.Save);
        env.Persisted = 0;
        env.Deleted.Clear();

        r.Delete(A);

        Assert.Equal(new[] { P("1_1_100_1.png"), P("1_1_100_2.png") }, env.Deleted.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(StepStatus.Pending, r.Session.RecordOf(A).Status);
        Assert.Null(r.Session.RecordOf(A).Frames);
        Assert.Empty(env.Files);
        Assert.Equal(1, env.Persisted);
    }

    [Fact]
    public void Skip_MarksSkipped_MovesOnAndSwitchesMagnification()
    {
        var (r, env, _) = New();

        r.Skip(A);

        Assert.Equal(StepStatus.Skipped, r.Session.RecordOf(A).Status);
        Assert.Equal(B, r.Current);
        Assert.Equal(200, env.CurrentMagnification);
        Assert.Equal(1, env.Persisted);
    }

    [Fact]
    public async Task MakeCurrent_OutOfPlanNumber_CapturedAsExtraStep()
    {
        var (r, env, saver) = New();
        var extra = new SeriesStepKey(7, 3, 500);

        r.MakeCurrent(extra);
        Assert.Equal(1, env.Persisted);
        Assert.Equal(500, env.CurrentMagnification);
        env.Calibrated.Add(500);
        await r.CaptureAsync(saver.Save);

        Assert.Equal(new[] { P("7_3_500_1.png"), P("7_3_500_2.png") }, env.Images);
        Assert.Equal(new[] { extra }, r.Session.ExtraSteps);
        Assert.Equal(A, r.Current);
        Assert.Equal(100, env.CurrentMagnification);
    }

    [Fact]
    public void Move_ChangesGroupAndSample_NotBelowOne()
    {
        var (r, _, _) = New();

        r.Move(0, 1);
        Assert.Equal(C, r.Current);
        r.Move(1, 0);
        Assert.Equal(new SeriesStepKey(2, 2, 100), r.Current);
        r.Move(-5, -5);
        Assert.Equal(A, r.Current);
    }

    [Fact]
    public async Task PartialSteps_KeepSeriesUnfinished_AndAreNextOnceCalibrated()
    {
        var (r, env, saver) = New();
        env.Calibrated.Remove(100);
        await r.CaptureAsync(saver.Save, cleanOnly: true);   // A частично
        await r.CaptureAsync(saver.Save);                    // B
        await r.CaptureAsync(saver.Save, cleanOnly: true);   // C частично

        Assert.Equal(2, r.PartialCount);
        Assert.False(SeriesRunner.IsFinished(r.Session));

        env.Calibrated.Add(100);
        await r.CaptureAsync(saver.Save);                    // D — неснятых больше нет

        Assert.True(r.Session.IsComplete);
        Assert.False(SeriesRunner.IsFinished(r.Session));
        Assert.Equal(A, r.Current);                          // следующий — первый частичный
        Assert.Equal(StepCaptureResult.Captured, await r.CaptureAsync(saver.Save));
        Assert.Equal(0, env.Questions);
        Assert.Equal(C, r.Current);
        await r.CaptureAsync(saver.Save);
        Assert.Equal(0, r.PartialCount);
        Assert.True(SeriesRunner.IsFinished(r.Session));
    }
}
