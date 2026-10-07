using LumeneraViewer.Imaging;

namespace LumeneraViewer.Tests;

public class FrameAnalysisTests
{
    // Шахматная доска 8×8 px клетки, чёрное/белое
    private static byte[] Checker(int w, int h, int cell = 8)
    {
        var px = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = ((x / cell + y / cell) % 2 == 0) ? (byte)0 : (byte)255;
                int i = (y * w + x) * 3;
                px[i] = px[i + 1] = px[i + 2] = v;
            }
        return px;
    }

    // Простое размытие 5×5 средним
    private static byte[] Blur(byte[] src, int w, int h)
    {
        var dst = new byte[src.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int c = 0; c < 3; c++)
                {
                    int sum = 0, n = 0;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int xx = Math.Clamp(x + dx, 0, w - 1), yy = Math.Clamp(y + dy, 0, h - 1);
                            sum += src[(yy * w + xx) * 3 + c]; n++;
                        }
                    dst[(y * w + x) * 3 + c] = (byte)(sum / n);
                }
        return dst;
    }

    [Fact]
    public void Histogram_SumEqualsSampleCount()
    {
        // 64×32 с шагом 4 → 16×8 = 128 выборок
        var h = FrameAnalysis.ComputeHistogram(Checker(64, 32), 64, 32, 4);
        Assert.Equal(128, h.SampleCount);
        Assert.Equal(128, h.R.Sum());
        Assert.Equal(128, h.G.Sum());
        Assert.Equal(128, h.B.Sum());
        Assert.Equal(128, h.Luma.Sum());
    }

    [Fact]
    public void Histogram_ClippedFraction_CountsPixelsWithAnyChannelAt255()
    {
        // Половина кадра (левая) имеет R=255, остальное серое 100
        int w = 8, hgt = 4;
        var px = new byte[w * hgt * 3];
        for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 3;
                px[i] = 100; px[i + 1] = 100; px[i + 2] = x < 4 ? (byte)255 : (byte)100;
            }
        var h = FrameAnalysis.ComputeHistogram(px, w, hgt, 1);
        Assert.Equal(0.5, h.ClippedFraction, 3);
    }

    [Fact]
    public void Sharpness_BlurredLowerThanSharp()
    {
        var sharp = Checker(128, 96);
        var blurred = Blur(sharp, 128, 96);
        double s1 = FrameAnalysis.ComputeSharpness(sharp, 128, 96);
        double s2 = FrameAnalysis.ComputeSharpness(blurred, 128, 96);
        Assert.True(s1 > s2 * 2, $"sharp={s1}, blurred={s2}");
    }

    [Fact]
    public void MeanLuma_IsNormalized()
    {
        var px = new byte[16 * 16 * 3];
        Array.Fill(px, (byte)51);   // 51/255 = 0.2
        Assert.Equal(0.2, FrameAnalysis.MeanLuma(px, 16, 16), 2);
    }
}
