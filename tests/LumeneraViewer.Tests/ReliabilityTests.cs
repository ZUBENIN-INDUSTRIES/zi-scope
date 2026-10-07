using System.Diagnostics;
using System.IO;
using LumeneraViewer.Drivers;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Tests;

/// <summary>Таск 08: буферы конвейера, троттлинг сохранения настроек, удаление спутников снимка, отмена установки.</summary>
public sealed class ReliabilityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lv-rel-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // ───── Буферы конвейера ─────

    [Fact]
    public void Pool_ReleasedFrame_BufferIsReused()
    {
        var pool = new FrameBufferPool();
        var a = pool.Rent(4, 2, 1, default);
        Assert.Equal(4 * 2 * 3, a.Bgr24.Length);
        pool.Release(a);
        var b = pool.Rent(4, 2, 2, default);
        Assert.Same(a.Bgr24, b.Bgr24);
    }

    [Fact]
    public void Pool_RetainedFrame_IsNotReusedUntilLastRelease()
    {
        var pool = new FrameBufferPool();
        var a = pool.Rent(4, 2, 1, default);
        Assert.True(pool.AddRef(a));     // кто-то взял кадр наружу
        pool.Release(a);                 // конвейер отпустил
        Assert.NotSame(a.Bgr24, pool.Rent(4, 2, 2, default).Bgr24);
        pool.Release(a);                 // внешний держатель отпустил
        Assert.Same(a.Bgr24, pool.Rent(4, 2, 3, default).Bgr24);
    }

    [Fact]
    public void Pool_EscapedFrame_IsNeverReused()
    {
        var pool = new FrameBufferPool();
        var a = pool.Rent(4, 2, 1, default);
        pool.Escape(a);
        pool.Release(a);
        Assert.False(pool.AddRef(a));
        Assert.NotSame(a.Bgr24, pool.Rent(4, 2, 2, default).Bgr24);
    }

    [Fact]
    public void Pool_SizeChange_AllocatesMatchingBuffer()
    {
        var pool = new FrameBufferPool();
        var a = pool.Rent(4, 2, 1, default);
        pool.Release(a);
        var b = pool.Rent(8, 2, 2, default);
        Assert.Equal(8 * 2 * 3, b.Bgr24.Length);
    }

    // ───── Троттлинг сохранения настроек ─────

    [Fact]
    public void SaveThrottle_ManyRequests_SerializeOncePerTimerTick()
    {
        var saves = 0;
        var t = new SaveThrottle(() => saves++);
        Assert.True(t.Request());        // первый запрос взводит таймер
        for (var i = 0; i < 50; i++) Assert.False(t.Request());
        Assert.Equal(0, saves);
        t.OnTimer();
        Assert.Equal(1, saves);
        t.OnTimer();                     // без новых изменений — не пишет
        Assert.Equal(1, saves);
    }

    [Fact]
    public void SaveThrottle_ChangeAfterTick_IsNotLost()
    {
        var saves = 0;
        var t = new SaveThrottle(() => saves++);
        t.Request();
        t.OnTimer();
        Assert.True(t.Request());        // таймер снова взводится
        t.Flush();                       // закрытие окна до тика
        Assert.Equal(2, saves);
        t.Flush();
        Assert.Equal(2, saves);
    }

    // ───── Удаление снимка из ленты ─────

    [Fact]
    public void DeleteShot_RemovesCleanCopyAndMetadata_KeepsOthers()
    {
        Directory.CreateDirectory(_dir);
        string F(string n) { var p = Path.Combine(_dir, n); File.WriteAllText(p, "x"); return p; }
        var main = F("Lumenera_20261007_140509_042.png");
        F("Lumenera_20261007_140509_042_clean.png");
        F("Lumenera_20261007_140509_042.json");
        var other = F("Lumenera_20261007_140510_000.png");

        GalleryViewModel.DeleteShot(main);

        Assert.Equal(new[] { other }, Directory.GetFiles(_dir));
    }

    // ───── Установка драйверов ─────

    private DriverService Service(Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<int>> run)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(Path.Combine(_dir, "setup.exe"), [0x4D, 0x5A]);
        return new DriverService(_dir, () => true, run);
    }

    [Fact]
    public async Task Install_CancelledByToken_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        var svc = Service(async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return 0; });
        var task = svc.InstallAsync(silent: true, cts.Token);
        cts.Cancel();
        Assert.Equal(DriverInstallResult.Cancelled, await task);
    }

    [Fact]
    public async Task Install_TimeoutAndInstallerCannotBeKilled_ReturnsStillRunning()
    {
        var svc = Service((_, _, _) => throw new InstallerStillRunningException());
        Assert.Equal(DriverInstallResult.TimedOutStillRunning, await svc.InstallAsync(silent: true));
    }
}
