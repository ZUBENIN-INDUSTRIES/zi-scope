namespace LumeneraViewer.Imaging;

// Шаг 7 конвейера: локальные операции по яркости (чёткость, удаление дымки, резкость) и шаг 8 после них.
public static partial class ImageProcessor
{
    /// <summary>Рабочие буферы размытия; один свободный набор переиспользуется между кадрами.</summary>
    private sealed class Workspace
    {
        public float[] Luma = [], Big = [], Small = [], Tmp = [];

        public void Ensure(int n, bool big, bool small)
        {
            if (Luma.Length < n) Luma = new float[n];
            if (Tmp.Length < n) Tmp = new float[n];
            if (big && Big.Length < n) Big = new float[n];
            if (small && Small.Length < n) Small = new float[n];
        }
    }

    private static Workspace? _spareWorkspace;

    private static unsafe void ApplyLocal(Span<byte> bgr, int w, int h, DeepColor d, bool gray, bool neg)
    {
        int n = w * h;
        bool big = d.Clarity != 0 || d.Dehaze != 0;
        bool small = d.SharpenAmount > 0;
        var ws = Interlocked.Exchange(ref _spareWorkspace, null) ?? new Workspace();
        try
        {
            ws.Ensure(n, big, small);
            fixed (byte* p = bgr)
            fixed (float* lp = ws.Luma) fixed (float* bp = ws.Big) fixed (float* sp = ws.Small) fixed (float* tp = ws.Tmp)
            {
                nint pix = (nint)p, L = (nint)lp, B = (nint)bp, S = (nint)sp, T = (nint)tp;

                Parallel.For(0, h, y =>
                {
                    byte* s = (byte*)pix + (long)y * w * 3;
                    float* o = (float*)L + (long)y * w;
                    for (int x = 0; x < w; x++, s += 3)
                        o[x] = LumaBf * s[0] + LumaGf * s[1] + LumaRf * s[2];
                });

                if (big)
                {
                    // Почти гауссово размытие большим радиусом: два прохода box-фильтра
                    int r = Math.Max(2, (int)Math.Round(Math.Max(w, h) / 40.0));
                    BoxH((float*)L, (float*)T, w, h, r);
                    BoxV((float*)T, (float*)B, w, h, r);
                    BoxH((float*)B, (float*)T, w, h, r);
                    BoxV((float*)T, (float*)B, w, h, r);
                }
                if (small)
                {
                    var k = GaussKernel(Math.Clamp(d.SharpenRadius, 0.5, 3));
                    GaussH((float*)L, (float*)T, w, h, k);
                    GaussV((float*)T, (float*)S, w, h, k);
                }

                float clarity = (float)(Math.Clamp(d.Clarity, -100, 100) / 100 * 0.8);
                float dehazeLocal = (float)(Math.Clamp(d.Dehaze, -100, 100) / 100 * 0.5);
                float sharp = (float)(Math.Clamp(d.SharpenAmount, 0, 150) / 100);
                byte[] haze = BuildDehazeLut(d.Dehaze);
                fixed (byte* hz = haze)
                {
                    nint H = (nint)hz;
                    Parallel.For(0, h, y =>
                    {
                        byte* px = (byte*)pix + (long)y * w * 3;
                        byte* HZ = (byte*)H;
                        long row = (long)y * w;
                        float* l = (float*)L + row;
                        float* bb = big ? (float*)B + row : null;
                        float* ss = small ? (float*)S + row : null;
                        for (int x = 0; x < w; x++, px += 3)
                        {
                            float lv = l[x], delta = 0;
                            if (bb != null)
                            {
                                float diff = lv - bb[x];
                                float m = lv / 127.5f - 1;              // чёткость сильнее в средних тонах
                                delta += diff * (clarity * (1 - m * m) + dehazeLocal);
                            }
                            if (ss != null) delta += sharp * (lv - ss[x]);
                            int dd = (int)MathF.Round(delta);
                            int b = HZ[Clamp255(px[0] + dd)], g = HZ[Clamp255(px[1] + dd)], r = HZ[Clamp255(px[2] + dd)];
                            if (gray) r = g = b = (LumaR * r + LumaG * g + LumaB * b + 128) >> 8;
                            if (neg) { r = 255 - r; g = 255 - g; b = 255 - b; }
                            px[0] = (byte)b; px[1] = (byte)g; px[2] = (byte)r;
                        }
                    });
                }
            }
        }
        finally
        {
            Volatile.Write(ref _spareWorkspace, ws);
        }
    }

    /// <summary>Удаление дымки, поточечная часть: &gt; 0 — подъём точки чёрного, &lt; 0 — добавление дымки.</summary>
    private static byte[] BuildDehazeLut(double dehaze)
    {
        double bp = Math.Clamp(dehaze, -100, 100) / 100 * 0.12;
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double v = i / 255.0;
            v = bp >= 0 ? (v - bp) / (1 - bp) : v * (1 + bp) - bp;
            lut[i] = (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        }
        return lut;
    }

    // Box-фильтр по строкам, края повторяются
    private static unsafe void BoxH(float* src, float* dst, int w, int h, int r)
    {
        nint S = (nint)src, D = (nint)dst;
        float inv = 1f / (2 * r + 1);
        Parallel.For(0, h, y =>
        {
            float* s = (float*)S + (long)y * w, o = (float*)D + (long)y * w;
            int last = w - 1;
            float sum = s[0] * (r + 1);
            for (int k = 1; k <= r; k++) sum += s[Math.Min(k, last)];
            for (int x = 0; x < w; x++)
            {
                o[x] = sum * inv;
                sum += s[Math.Min(x + r + 1, last)] - s[Math.Max(x - r, 0)];
            }
        });
    }

    // Box-фильтр по столбцам: полосы по 64 столбца, бегущая сумма по строкам
    private static unsafe void BoxV(float* src, float* dst, int w, int h, int r)
    {
        const int Band = 64;
        nint S = (nint)src, D = (nint)dst;
        float inv = 1f / (2 * r + 1);
        int bands = (w + Band - 1) / Band;
        Parallel.For(0, bands, band =>
        {
            int x0 = band * Band, bw = Math.Min(Band, w - x0), last = h - 1;
            float* s = (float*)S + x0, o = (float*)D + x0;
            float* sum = stackalloc float[Band];
            for (int x = 0; x < bw; x++) sum[x] = s[x] * (r + 1);
            for (int k = 1; k <= r; k++)
            {
                float* row = s + (long)Math.Min(k, last) * w;
                for (int x = 0; x < bw; x++) sum[x] += row[x];
            }
            for (int y = 0; y < h; y++)
            {
                float* orow = o + (long)y * w;
                float* add = s + (long)Math.Min(y + r + 1, last) * w;
                float* sub = s + (long)Math.Max(y - r, 0) * w;
                for (int x = 0; x < bw; x++)
                {
                    orow[x] = sum[x] * inv;
                    sum[x] += add[x] - sub[x];
                }
            }
        });
    }

    private static float[] GaussKernel(double sigma)
    {
        int half = Math.Max(1, (int)Math.Ceiling(sigma * 3));
        var k = new float[2 * half + 1];
        double total = 0;
        for (int i = -half; i <= half; i++) total += Math.Exp(-i * i / (2 * sigma * sigma));
        for (int i = -half; i <= half; i++) k[i + half] = (float)(Math.Exp(-i * i / (2 * sigma * sigma)) / total);
        return k;
    }

    private static unsafe void GaussH(float* src, float* dst, int w, int h, float[] k)
    {
        nint S = (nint)src, D = (nint)dst;
        int half = k.Length / 2;
        Parallel.For(0, h, y =>
        {
            float* s = (float*)S + (long)y * w, o = (float*)D + (long)y * w;
            int last = w - 1;
            for (int x = 0; x < w; x++)
            {
                float acc = 0;
                if (x >= half && x + half <= last)
                {
                    float* c = s + x - half;
                    for (int j = 0; j < k.Length; j++) acc += k[j] * c[j];
                }
                else
                {
                    for (int j = 0; j < k.Length; j++) acc += k[j] * s[Math.Clamp(x + j - half, 0, last)];
                }
                o[x] = acc;
            }
        });
    }

    private static unsafe void GaussV(float* src, float* dst, int w, int h, float[] k)
    {
        nint S = (nint)src, D = (nint)dst;
        int half = k.Length / 2;
        Parallel.For(0, h, y =>
        {
            float* o = (float*)D + (long)y * w;
            int last = h - 1;
            new Span<float>(o, w).Clear();
            for (int j = 0; j < k.Length; j++)
            {
                float kj = k[j];
                float* s = (float*)S + (long)Math.Clamp(y + j - half, 0, last) * w;
                for (int x = 0; x < w; x++) o[x] += kj * s[x];
            }
        });
    }
}
