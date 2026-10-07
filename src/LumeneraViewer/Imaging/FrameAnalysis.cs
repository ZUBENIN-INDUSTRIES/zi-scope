namespace LumeneraViewer.Imaging;

public sealed class Histogram
{
    public int[] R { get; } = new int[256];
    public int[] G { get; } = new int[256];
    public int[] B { get; } = new int[256];
    public int[] Luma { get; } = new int[256];
    /// <summary>Число выборок (сумма любого из массивов).</summary>
    public int SampleCount { get; set; }
    /// <summary>Доля выборок, у которых хотя бы один канал = 255.</summary>
    public double ClippedFraction { get; set; }
}

/// <summary>Анализ кадра BGR24: гистограмма, резкость, средняя яркость.</summary>
public static class FrameAnalysis
{
    public static Histogram ComputeHistogram(ReadOnlySpan<byte> bgr, int w, int h, int step = 4)
    {
        step = Math.Max(1, step);
        var hist = new Histogram();
        int n = 0, clipped = 0;
        for (int y = 0; y < h; y += step)
        {
            int row = y * w * 3;
            for (int x = 0; x < w; x += step)
            {
                int i = row + x * 3;
                byte b = bgr[i], g = bgr[i + 1], r = bgr[i + 2];
                hist.B[b]++; hist.G[g]++; hist.R[r]++;
                hist.Luma[Luma(r, g, b)]++;
                if (r == 255 || g == 255 || b == 255) clipped++;
                n++;
            }
        }
        hist.SampleCount = n;
        hist.ClippedFraction = n == 0 ? 0 : (double)clipped / n;
        return hist;
    }

    /// <summary>Дисперсия лапласиана яркости в центральной области (больше — резче).</summary>
    public static double ComputeSharpness(ReadOnlySpan<byte> bgr, int w, int h, double roiFraction = 0.5)
    {
        var (x0, y0, rw, rh) = ImageProcessor.Roi(w, h, roiFraction);
        int xs = Math.Max(1, x0), xe = Math.Min(w - 1, x0 + rw);
        int ys = Math.Max(1, y0), ye = Math.Min(h - 1, y0 + rh);
        double sum = 0, sum2 = 0;
        long n = 0;
        for (int y = ys; y < ye; y++)
            for (int x = xs; x < xe; x++)
            {
                int lap = 4 * LumaAt(bgr, w, x, y)
                          - LumaAt(bgr, w, x - 1, y) - LumaAt(bgr, w, x + 1, y)
                          - LumaAt(bgr, w, x, y - 1) - LumaAt(bgr, w, x, y + 1);
                sum += lap; sum2 += (double)lap * lap; n++;
            }
        if (n == 0) return 0;
        double mean = sum / n;
        return sum2 / n - mean * mean;
    }

    /// <summary>Средняя яркость (Rec.709) центральной области, 0…1.</summary>
    public static double MeanLuma(ReadOnlySpan<byte> bgr, int w, int h, double roiFraction = 0.5)
    {
        var (x0, y0, rw, rh) = ImageProcessor.Roi(w, h, roiFraction);
        long sum = 0;
        for (int y = y0; y < y0 + rh; y++)
            for (int x = x0; x < x0 + rw; x++)
                sum += LumaAt(bgr, w, x, y);
        return sum / (255.0 * rw * rh);
    }

    private static int LumaAt(ReadOnlySpan<byte> bgr, int w, int x, int y)
    {
        int i = (y * w + x) * 3;
        return Luma(bgr[i + 2], bgr[i + 1], bgr[i]);
    }

    private static int Luma(int r, int g, int b)
        => (ImageProcessor.LumaR * r + ImageProcessor.LumaG * g + ImageProcessor.LumaB * b + 128) >> 8;
}
