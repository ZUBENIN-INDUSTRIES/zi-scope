using LumeneraViewer.Series;

namespace LumeneraViewer.Tests;

public sealed class SeriesSessionTests
{
    // 1 группа × 2 образца × [100, 200]: (1,1,100) (1,1,200) (1,2,100) (1,2,200)
    private static readonly SeriesStepKey A = new(1, 1, 100), B = new(1, 1, 200), C = new(1, 2, 100), D = new(1, 2, 200);

    private static SeriesSession New() =>
        new(new SeriesPlan("s", 1, 2, new[] { 100, 200 }, SeriesPlan.DefaultKinds, SeriesOrder.SampleBySample), @"C:\series");

    private static string[] Files(string stem) => new[] { stem + "_1.png", stem + "_2.png" };

    [Fact]
    public void New_AllPending_CurrentIsFirst()
    {
        var s = New();
        Assert.Equal(A, s.Current);
        Assert.False(s.Reshooting);
        Assert.Equal(0, s.DoneCount);
        Assert.False(s.IsComplete);
        Assert.Equal(StepStatus.Pending, s.RecordOf(D).Status);
        Assert.Empty(s.ExtraSteps);
        Assert.Equal(A, s.NextPending());
    }

    [Fact]
    public void MarkDone_StoresFiles_AndAdvancesToNextPending()
    {
        var s = New();
        s.MarkDone(A, Files("a"));
        Assert.Equal(StepStatus.Done, s.RecordOf(A).Status);
        Assert.Equal(Files("a"), s.RecordOf(A).Files);
        Assert.NotNull(s.RecordOf(A).When);
        Assert.Equal(B, s.Current);
        Assert.Equal(1, s.DoneCount);
    }

    [Fact]
    public void MarkSkipped_AdvancesAndNextPendingWrapsAround()
    {
        var s = New();
        s.MarkSkipped(A);
        Assert.Equal(StepStatus.Skipped, s.RecordOf(A).Status);
        Assert.Equal(B, s.Current);
        s.MarkDone(B, Files("b"));
        s.MarkDone(C, Files("c"));
        Assert.Equal(D, s.Current);
        s.MarkDone(D, Files("d"));
        // неснятых нет: A пропущен — тоже не «неснятый»
        Assert.Null(s.NextPending(D));
        s.Reset(A);
        Assert.Equal(A, s.NextPending(D));   // поиск идёт по кругу
        Assert.Equal(StepStatus.Pending, s.RecordOf(A).Status);
    }

    [Fact]
    public void Reset_ClearsFiles()
    {
        var s = New();
        s.MarkDone(A, Files("a"));
        s.Reset(A);
        Assert.Equal(StepStatus.Pending, s.RecordOf(A).Status);
        Assert.Empty(s.RecordOf(A).Files);
        Assert.Equal(0, s.DoneCount);
    }

    [Fact]
    public void Reshoot_NextMarkDoneReturnsToFirstPending()
    {
        var s = New();
        s.MarkDone(A, Files("a"));
        s.MarkDone(B, Files("b"));
        Assert.Equal(C, s.Current);

        s.BeginReshoot(A);
        Assert.Equal(A, s.Current);
        Assert.True(s.Reshooting);

        s.MarkDone(A, Files("a2"));
        Assert.False(s.Reshooting);
        Assert.Equal(C, s.Current);          // не B (он снят), а первый неснятый
        Assert.Equal(Files("a2"), s.RecordOf(A).Files);
        Assert.Equal(2, s.DoneCount);
    }

    [Fact]
    public void OffPlanStep_GoesToExtraSteps_NotIntoProgress()
    {
        var s = New();
        var extra = new SeriesStepKey(7, 3, 500);
        s.SetCurrent(extra);
        Assert.Equal(extra, s.Current);
        s.MarkDone(extra, Files("x"));
        Assert.Equal(new[] { extra }, s.ExtraSteps);
        Assert.Equal(StepStatus.Done, s.RecordOf(extra).Status);
        Assert.Equal(0, s.DoneCount);        // прогресс — только по шагам плана
        Assert.Equal(A, s.Current);          // вернулись к первому неснятому

        s.Reset(extra);
        Assert.Empty(s.ExtraSteps);
    }

    [Fact]
    public void ManualNumberInsidePlan_MarksThatPlanStep()
    {
        var s = New();
        s.SetCurrent(C);
        s.MarkDone(C, Files("c"));
        Assert.Equal(StepStatus.Done, s.RecordOf(C).Status);
        Assert.Empty(s.ExtraSteps);
        Assert.Equal(D, s.Current);
    }

    [Fact]
    public void AllDoneOrSkipped_IsComplete()
    {
        var s = New();
        s.MarkDone(A, Files("a"));
        s.MarkDone(B, Files("b"));
        s.MarkSkipped(C);
        Assert.False(s.IsComplete);
        s.MarkDone(D, Files("d"));
        Assert.True(s.IsComplete);
        Assert.Equal(3, s.DoneCount);
        Assert.Null(s.NextPending());
        Assert.Equal(D, s.Current);          // идти некуда — остаёмся на последнем
    }

    [Fact]
    public void SkipOffPlanStep_NotAddedToExtra_NoRecord_ReturnsToFirstPending()
    {
        var s = New();
        s.MarkDone(A, Files("a"));
        var extra = new SeriesStepKey(7, 3, 500);
        s.SetCurrent(extra);
        s.MarkSkipped(extra);
        Assert.Empty(s.ExtraSteps);
        Assert.False(s.Records.ContainsKey(extra));
        Assert.Equal(StepStatus.Pending, s.RecordOf(extra).Status);
        Assert.Equal(B, s.Current);
    }
}
