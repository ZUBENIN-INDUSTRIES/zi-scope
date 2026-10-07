namespace LumeneraViewer.Imaging;

/// <summary>Программная цветокоррекция BGR24: 3 LUT (баланс белого, яркость, контраст, гамма) + насыщенность, Ч/Б, негатив.</summary>
public static class ImageProcessor
{
    // Rec.709 в фиксированной точке (сумма 256)
    internal const int LumaR = 54, LumaG = 183, LumaB = 19;

    public static unsafe void Apply(ReadOnlySpan<byte> srcBgr, Span<byte> dstBgr, int w, int h, ImageAdjustments a)
    {
        int len = w * h * 3;
        if (srcBgr.Length < len || dstBgr.Length < len)
            throw new ArgumentException("Размер буфера меньше w·h·3");

        if (a.IsIdentity)
        {
            srcBgr[..len].CopyTo(dstBgr);
            return;
        }

        double kR = 1 + 0.003 * a.Temperature;
        double kB = 1 - 0.003 * a.Temperature;
        double kG = 1 - 0.003 * a.Tint;
        byte[] lutR = BuildLut(kR, a), lutG = BuildLut(kG, a), lutB = BuildLut(kB, a);

        int satFix = (int)Math.Round(Math.Clamp(a.Saturation, 0, 200) / 100.0 * 256);
        bool doSat = satFix != 256 && !a.Grayscale;
        bool gray = a.Grayscale, neg = a.Negative;
        int stride = w * 3;

        fixed (byte* sp = srcBgr)
        fixed (byte* dp = dstBgr)
        fixed (byte* lr = lutR)
        fixed (byte* lg = lutG)
        fixed (byte* lb = lutB)
        {
            nint s0 = (nint)sp, d0 = (nint)dp, pr = (nint)lr, pg = (nint)lg, pb = (nint)lb;
            Parallel.For(0, h, y =>
            {
                byte* s = (byte*)s0 + (long)y * stride;
                byte* d = (byte*)d0 + (long)y * stride;
                byte* LR = (byte*)pr, LG = (byte*)pg, LB = (byte*)pb;
                for (int x = 0; x < w; x++, s += 3, d += 3)
                {
                    int b = LB[s[0]], g = LG[s[1]], r = LR[s[2]];
                    if (gray || doSat)
                    {
                        int l = (LumaR * r + LumaG * g + LumaB * b + 128) >> 8;
                        if (gray)
                        {
                            r = g = b = l;
                        }
                        else
                        {
                            r = Clamp255(l + (r - l) * satFix / 256);
                            g = Clamp255(l + (g - l) * satFix / 256);
                            b = Clamp255(l + (b - l) * satFix / 256);
                        }
                    }
                    if (neg) { r = 255 - r; g = 255 - g; b = 255 - b; }
                    d[0] = (byte)b; d[1] = (byte)g; d[2] = (byte)r;
                }
            });
        }
    }

    private static int Clamp255(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    // Шаги 1–5 формул спецификации для одного канала
    private static byte[] BuildLut(double k, ImageAdjustments a)
    {
        var lut = new byte[256];
        double c = (a.Contrast + 100) / 100;
        c = c < 1 ? c : 1 + (c - 1) * 2;
        double invGamma = 1 / Math.Max(0.01, a.Gamma);
        for (int i = 0; i < 256; i++)
        {
            double v = Math.Clamp(i / 255.0 * k, 0, 1);
            v += a.Brightness / 200;
            v = (v - 0.5) * c + 0.5;
            v = Math.Pow(Math.Clamp(v, 0, 1), invGamma);
            lut[i] = (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        }
        return lut;
    }

    /// <summary>Коэффициенты «серого мира» по центральной области (G = 1).</summary>
    public static (double r, double g, double b) ComputeGrayWorldGains(ReadOnlySpan<byte> bgr, int w, int h, double roiFraction = 0.5)
    {
        var (x0, y0, rw, rh) = Roi(w, h, roiFraction);
        long sr = 0, sg = 0, sb = 0;
        for (int y = y0; y < y0 + rh; y++)
        {
            int i = (y * w + x0) * 3;
            for (int x = 0; x < rw; x++, i += 3) { sb += bgr[i]; sg += bgr[i + 1]; sr += bgr[i + 2]; }
        }
        if (sr == 0 || sg == 0 || sb == 0) return (1, 1, 1);
        return ((double)sg / sr, 1, (double)sg / sb);
    }

    /// <summary>Обратное к шагу 1 формул: коэффициенты → Temperature/Tint, обрезка до ±100.</summary>
    public static (double temperature, double tint) GainsToTemperatureTint((double r, double g, double b) gains)
    {
        double m = (gains.r + gains.b) / 2;
        if (m <= 0) return (0, 0);
        double kR = gains.r / m, kB = gains.b / m, kG = gains.g / m;
        double t = (kR - kB) / 0.006;
        double tint = (1 - kG) / 0.003;
        return (Math.Clamp(t, -100, 100), Math.Clamp(tint, -100, 100));
    }

    internal static (int x0, int y0, int w, int h) Roi(int w, int h, double fraction)
    {
        fraction = Math.Clamp(fraction, 0.01, 1);
        int rw = Math.Max(1, (int)(w * fraction)), rh = Math.Max(1, (int)(h * fraction));
        return ((w - rw) / 2, (h - rh) / 2, rw, rh);
    }
}
