using LumeneraViewer.Imaging;

namespace LumeneraViewer.Tests;

public class ImageProcessorTests
{
    // Кадр 4×2 одного цвета (B,G,R)
    private static byte[] Solid(byte b, byte g, byte r, int w = 4, int h = 2)
    {
        var px = new byte[w * h * 3];
        for (int i = 0; i < px.Length; i += 3) { px[i] = b; px[i + 1] = g; px[i + 2] = r; }
        return px;
    }

    private static byte[] Run(byte[] src, ImageAdjustments a, int w = 4, int h = 2)
    {
        var dst = new byte[src.Length];
        ImageProcessor.Apply(src, dst, w, h, a);
        return dst;
    }

    [Fact]
    public void Identity_KeepsPixels()
    {
        var src = new byte[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220, 255 };
        Assert.Equal(src, Run(src, ImageAdjustments.Default));
    }

    [Fact]
    public void Gamma2_Brightens64ToAbout128()
    {
        // (64/255)^(1/2)·255 ≈ 127.7
        var dst = Run(Solid(64, 64, 64), ImageAdjustments.Default with { Gamma = 2 });
        Assert.InRange((int)dst[0], 126, 129);
    }

    [Fact]
    public void ContrastPlus100_StretchesAwayFrom128()
    {
        // c = 3: 160 → ((160/255 − 0.5)·3 + 0.5)·255 ≈ 225; 96 → ≈ 33
        var hi = Run(Solid(160, 160, 160), ImageAdjustments.Default with { Contrast = 100 });
        var lo = Run(Solid(96, 96, 96), ImageAdjustments.Default with { Contrast = 100 });
        Assert.InRange((int)hi[1], 224, 226);
        Assert.InRange((int)lo[1], 31, 35);
    }

    [Fact]
    public void SaturationZero_GivesGray()
    {
        var dst = Run(Solid(30, 120, 220), ImageAdjustments.Default with { Saturation = 0 });
        Assert.Equal(dst[0], dst[1]);
        Assert.Equal(dst[1], dst[2]);
        // L = 0.2126·220 + 0.7152·120 + 0.0722·30 ≈ 134
        Assert.InRange((int)dst[0], 132, 136);
    }

    [Fact]
    public void Negative_Inverts()
    {
        var dst = Run(Solid(10, 100, 250), ImageAdjustments.Default with { Negative = true });
        Assert.Equal(new byte[] { 245, 155, 5 }, dst[..3]);
    }

    [Fact]
    public void WarmTemperature_RaisesRedLowersBlue()
    {
        var dst = Run(Solid(128, 128, 128), ImageAdjustments.Default with { Temperature = 50 });
        Assert.True(dst[2] > 128, $"R={dst[2]}");
        Assert.True(dst[0] < 128, $"B={dst[0]}");
        Assert.InRange((int)dst[1], 127, 129);
    }

    [Fact]
    public void BlueCast_GivesPositiveTemperature()
    {
        var frame = Solid(200, 150, 110, 16, 16);
        var gains = ImageProcessor.ComputeGrayWorldGains(frame, 16, 16);
        var (t, _) = ImageProcessor.GainsToTemperatureTint(gains);
        Assert.True(t > 0, $"Temperature={t}");
        Assert.InRange(t, 0.1, 100);
    }
}
