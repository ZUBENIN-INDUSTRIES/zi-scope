using LumeneraViewer.Camera;
using Xunit;

namespace LumeneraViewer.Tests;

public class SimulatedResolutionTests
{
    [Theory]
    [InlineData(1280, 960)]
    [InlineData(2004, 1336)]
    public void Frames_HaveRequestedSize(int w, int h)
    {
        using var cam = new SimulatedCamera(w, h);
        CameraFrame? got = null;
        using var done = new ManualResetEventSlim();
        cam.FrameReceived += f => { got ??= f; done.Set(); };
        cam.Start();
        Assert.True(done.Wait(10000));
        cam.Stop();
        Assert.Equal(w, got!.Width);
        Assert.Equal(h, got.Height);
        Assert.Equal(w * h * 3, got.Bgr24.Length);
        Assert.Equal(w, cam.Info?.Width ?? w);
    }

    [Fact]
    public void Sides_AreClamped()
    {
        using var cam = new SimulatedCamera(10, 100000);
        cam.Open();
        Assert.Equal(SimulatedCamera.MinSide, cam.Info!.Width);
        Assert.Equal(SimulatedCamera.MaxSide, cam.Info.Height);
    }
}
