using LumeneraViewer.Camera;
using LumeneraViewer.Imaging;

namespace LumeneraViewer.Tests;

public class SimulatedCameraTests
{
    // Ждёт кадр, пришедший после момента вызова (пропуская уже готовые)
    private static CameraFrame NextFrame(SimulatedCamera cam, int skip = 2)
    {
        var done = new ManualResetEventSlim();
        CameraFrame? got = null;
        int seen = 0;
        void Handler(CameraFrame f)
        {
            if (++seen <= skip) return;
            got = f;
            done.Set();
        }
        cam.FrameReceived += Handler;
        try
        {
            Assert.True(done.Wait(5000), "кадр не пришёл за 5 с");
            return got!;
        }
        finally { cam.FrameReceived -= Handler; }
    }

    [Fact]
    public void Info_And_Ranges()
    {
        using var cam = new SimulatedCamera();
        cam.Open();
        Assert.Equal(new CameraInfo("Симулятор INFINITY 4", "DEMO", 1920, 1440, true), cam.Info);
        Assert.Equal(new PropertyRange(0.1f, 2000f, 30f), cam.GetRange(CameraProperty.Exposure));
        Assert.Equal(new PropertyRange(1f, 16f, 1f), cam.GetRange(CameraProperty.Gain));
        Assert.Equal(30f, cam.Get(CameraProperty.Exposure));
    }

    [Fact]
    public void Streams_FullSizeBgrFrames()
    {
        using var cam = new SimulatedCamera();
        cam.Open();
        cam.Start();
        Assert.True(cam.IsStreaming);
        var f = NextFrame(cam, 0);
        Assert.Equal(1920, f.Width);
        Assert.Equal(1440, f.Height);
        Assert.Equal(1920 * 1440 * 3, f.Bgr24.Length);
        var f2 = NextFrame(cam, 0);
        Assert.True(f2.Sequence > f.Sequence);
        cam.Stop();
        Assert.False(cam.IsStreaming);
    }

    [Fact]
    public void Exposure_And_Focus_ChangeTheImage()
    {
        using var cam = new SimulatedCamera();
        cam.Open();
        cam.Start();
        Assert.True(cam.Set(CameraProperty.Exposure, 10));
        var dark = NextFrame(cam);
        Assert.True(cam.Set(CameraProperty.Exposure, 30));
        var normal = NextFrame(cam);
        Assert.True(FrameAnalysis.MeanLuma(dark.Bgr24, 1920, 1440) < FrameAnalysis.MeanLuma(normal.Bgr24, 1920, 1440) * 0.6);

        cam.Focus = 1f;
        var blurred = NextFrame(cam);
        Assert.True(FrameAnalysis.ComputeSharpness(blurred.Bgr24, 1920, 1440) < FrameAnalysis.ComputeSharpness(normal.Bgr24, 1920, 1440));
    }
}
