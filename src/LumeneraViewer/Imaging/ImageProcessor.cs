using System.Runtime.CompilerServices;

namespace LumeneraViewer.Imaging;

/// <summary>
/// Программная цветокоррекция BGR24. Порядок (спецификация, Решения §4): (1) баланс белого и усиление RGB;
/// (2) экспонирование; (3) тон по яркости; (4) уровни и тональные кривые; (5) яркость/контраст/гамма;
/// (6) HSL, красочность, насыщенность; (7) чёткость, дымка, резкость; (8) Ч/Б и негатив.
/// Шаги 1–5 — таблицы (LUT), 7 считается только при ненулевых значениях.
/// </summary>
public static partial class ImageProcessor
{
    // Rec.709 в фиксированной точке (сумма 256)
    internal const int LumaR = 54, LumaG = 183, LumaB = 19;
    private const float LumaRf = 0.2126f, LumaGf = 0.7152f, LumaBf = 0.0722f;

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

        var deep = a.Deep;
        bool tone = deep.HasTone;
        bool hsl = deep.HasHslOrVibrance;
        bool local = deep.HasLocalOps;

        double kR = 1 + 0.003 * a.Temperature;
        double kB = 1 - 0.003 * a.Temperature;
        double kG = 1 - 0.003 * a.Tint;
        var pr = Pre(kR * deep.RedGain, deep);
        var pg = Pre(kG * deep.GreenGain, deep);
        var pb = Pre(kB * deep.BlueGain, deep);
        var qr = Post(deep.Red, deep.CurveRed, deep, a);
        var qg = Post(deep.Green, deep.CurveGreen, deep, a);
        var qb = Post(deep.Blue, deep.CurveBlue, deep, a);

        // Без тона шаги 1–5 сводятся в одну таблицу на канал (без промежуточного округления); с тоном — две
        byte[] lutR, lutG, lutB, preR, preG, preB;
        int[] toneDelta;
        if (tone)
        {
            preR = Lut(pr); preG = Lut(pg); preB = Lut(pb);
            lutR = Lut(qr); lutG = Lut(qg); lutB = Lut(qb);
            toneDelta = BuildToneDelta(deep);
        }
        else
        {
            lutR = Lut(v => qr(pr(v))); lutG = Lut(v => qg(pg(v))); lutB = Lut(v => qb(pb(v)));
            preR = preG = preB = lutR;
            toneDelta = new int[256];
        }

        float baseSat = (float)(Math.Clamp(a.Saturation, 0, 200) / 100.0);
        int satFix = (int)Math.Round(baseSat * 256);
        bool gray = a.Grayscale, neg = a.Negative;
        bool doSat = satFix != 256 && !gray && !hsl;
        bool lateFinish = local;            // Ч/Б и негатив — после локальных операций
        var hue = hsl ? HueTable.Build(deep) : null;
        float vib = (float)(Math.Clamp(deep.Vibrance, -100, 100) / 100.0);
        float satAll = gray ? 1f : baseSat;
        int stride = w * 3;

        fixed (byte* sp = srcBgr)
        fixed (byte* dp = dstBgr)
        fixed (byte* lr = lutR) fixed (byte* lg = lutG) fixed (byte* lb = lutB)
        fixed (byte* er = preR) fixed (byte* eg = preG) fixed (byte* eb = preB)
        fixed (int* td = toneDelta)
        {
            nint s0 = (nint)sp, d0 = (nint)dp, plr = (nint)lr, plg = (nint)lg, plb = (nint)lb,
                 per = (nint)er, peg = (nint)eg, peb = (nint)eb, ptd = (nint)td;
            Parallel.For(0, h, y =>
            {
                byte* s = (byte*)s0 + (long)y * stride;
                byte* d = (byte*)d0 + (long)y * stride;
                byte* LR = (byte*)plr, LG = (byte*)plg, LB = (byte*)plb;
                byte* ER = (byte*)per, EG = (byte*)peg, EB = (byte*)peb;
                int* TD = (int*)ptd;
                for (int x = 0; x < w; x++, s += 3, d += 3)
                {
                    int b, g, r;
                    if (tone)
                    {
                        b = EB[s[0]]; g = EG[s[1]]; r = ER[s[2]];
                        int t = TD[(LumaR * r + LumaG * g + LumaB * b + 128) >> 8];
                        b = LB[Clamp255(b + t)]; g = LG[Clamp255(g + t)]; r = LR[Clamp255(r + t)];
                    }
                    else
                    {
                        b = LB[s[0]]; g = LG[s[1]]; r = LR[s[2]];
                    }

                    if (hue is not null)
                        ApplyHsl(ref r, ref g, ref b, hue, vib, satAll);
                    else if (doSat)
                    {
                        int l = (LumaR * r + LumaG * g + LumaB * b + 128) >> 8;
                        r = Clamp255(l + (r - l) * satFix / 256);
                        g = Clamp255(l + (g - l) * satFix / 256);
                        b = Clamp255(l + (b - l) * satFix / 256);
                    }

                    if (!lateFinish)
                    {
                        if (gray) r = g = b = (LumaR * r + LumaG * g + LumaB * b + 128) >> 8;
                        if (neg) { r = 255 - r; g = 255 - g; b = 255 - b; }
                    }
                    d[0] = (byte)b; d[1] = (byte)g; d[2] = (byte)r;
                }
            });
        }

        if (local) ApplyLocal(dstBgr[..len], w, h, deep, gray, neg);
    }

    private static int Clamp255(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    private static byte[] Lut(Func<double, double> f)
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++) lut[i] = (byte)Math.Round(Math.Clamp(f(i / 255.0), 0, 1) * 255);
        return lut;
    }

    // Шаги 1–2: баланс белого × усиление канала, экспонирование (×2^EV в линейном свете ≈ ×2^(EV/2.2) в гамме 2.2)
    private static Func<double, double> Pre(double k, DeepColor d)
    {
        double e = d.ExposureEv == 0 ? 1 : Math.Pow(2, Math.Clamp(d.ExposureEv, -3, 3) / 2.2);
        double kk = k * e;
        return v => Math.Clamp(v * kk, 0, 1);
    }

    // Шаги 4–5: уровни канала, затем общие; кривая общая, затем канала; затем яркость/контраст/гамма
    private static Func<double, double> Post(ChannelLevels levels, ToneCurve curve, DeepColor d, ImageAdjustments a)
    {
        var master = d.Master;
        var cm = d.CurveMaster.IsIdentity ? null : d.CurveMaster.CreateEvaluator();
        var cc = curve.IsIdentity ? null : curve.CreateEvaluator();
        double c = (a.Contrast + 100) / 100;
        c = c < 1 ? c : 1 + (c - 1) * 2;
        double invGamma = 1 / Math.Max(0.01, a.Gamma);
        double bright = a.Brightness / 200;
        return v =>
        {
            v = master.Map(levels.Map(v));
            if (cm is not null) v = Math.Clamp(cm(v * 255) / 255, 0, 1);
            if (cc is not null) v = Math.Clamp(cc(v * 255) / 255, 0, 1);
            v += bright;
            v = (v - 0.5) * c + 0.5;
            return Math.Pow(Math.Clamp(v, 0, 1), invGamma);
        };
    }

    /// <summary>Шаг 3: добавка к каналам по яркости (0…255). Маски плавные, итоговая тоновая кривая монотонна.</summary>
    private static int[] BuildToneDelta(DeepColor d)
    {
        double hl = Math.Clamp(d.Highlights, -100, 100) / 100, sh = Math.Clamp(d.Shadows, -100, 100) / 100;
        double wh = Math.Clamp(d.Whites, -100, 100) / 100, bl = Math.Clamp(d.Blacks, -100, 100) / 100;
        var delta = new int[256];
        double prev = 0;
        for (int i = 0; i < 256; i++)
        {
            double l = i / 255.0, il = 1 - l;
            double t = l
                + 0.25 * sh * 6.75 * l * il * il      // тени: пик на 1/3, чёрное и белое на месте
                + 0.25 * hl * 6.75 * l * l * il       // светлые: пик на 2/3
                + 0.20 * wh * l * l * l * l           // белые: двигают верхний край
                + 0.20 * bl * il * il * il * il;      // чёрные: двигают нижний край
            t = Math.Clamp(t, 0, 1);
            if (t < prev) t = prev;
            prev = t;
            delta[i] = (int)Math.Round((t - l) * 255);
        }
        return delta;
    }

    /// <summary>Таблица HSL по оттенку (ячейка = 1°): сдвиг оттенка в шестых долях круга, множитель насыщенности, добавка светлоты.</summary>
    private sealed class HueTable
    {
        // Центры секторов: красный, оранжевый, жёлтый, зелёный, голубой, синий, фиолетовый, пурпурный
        private static readonly double[] Centers = [0, 30, 60, 120, 180, 240, 270, 300];

        public readonly float[] Shift = new float[360], Sat = new float[360], Lum = new float[360];

        public static HueTable Build(DeepColor d)
        {
            var t = new HueTable();
            var hsl = d.Hsl;
            for (int deg = 0; deg < 360; deg++)
            {
                double hue = deg + 0.5;
                int i = Centers.Length - 1;
                while (i > 0 && hue < Centers[i]) i--;
                int j = (i + 1) % Centers.Length;
                double c0 = Centers[i], c1 = j == 0 ? 360 : Centers[j];
                double u = (hue - c0) / (c1 - c0);
                double wj = u * u * (3 - 2 * u), wi = 1 - wj;      // плавный переход между соседними секторами
                HslAdjust a = hsl[i], b = hsl[j];
                double shiftDeg = Math.Clamp((wi * a.Hue + wj * b.Hue) / 100 * 30, -30, 30);
                t.Shift[deg] = (float)(shiftDeg / 60);
                t.Sat[deg] = (float)Math.Max(0, 1 + (wi * a.Saturation + wj * b.Saturation) / 100);
                t.Lum[deg] = (float)((wi * a.Luminance + wj * b.Luminance) / 100 * 0.3 * 255);
            }
            return t;
        }
    }

    /// <summary>Шаг 6: HSL по секторам, красочность (сильнее для малонасыщенных), общая насыщенность.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyHsl(ref int r, ref int g, ref int b, HueTable t, float vib, float baseSat)
    {
        int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), c = mx - mn;
        if (c == 0) return;   // серый: оттенка нет, насыщенность нулевая
        float fc = c;
        float hh = mx == r ? (g - b) / fc : mx == g ? 2 + (b - r) / fc : 4 + (r - g) / fc;
        if (hh < 0) hh += 6;
        // Ячейка k таблицы — оттенок k+0.5°; между ячейками — линейная интерполяция (без ступенек)
        float pos = hh * 60 - 0.5f;
        if (pos < 0) pos += 360;
        int i0 = (int)pos;
        if (i0 >= 360) i0 = 359;
        int i1 = i0 == 359 ? 0 : i0 + 1;
        float u = pos - i0;

        float fr = r, fg = g, fb = b;
        float shift = t.Shift[i0] + (t.Shift[i1] - t.Shift[i0]) * u;
        if (shift != 0)
        {
            float h2 = hh + shift;
            if (h2 < 0) h2 += 6; else if (h2 >= 6) h2 -= 6;
            int sector = Math.Min((int)h2, 5);
            float m2 = h2 - (sector & ~1);                     // h2 mod 2
            float xx = mn + fc * (1 - MathF.Abs(m2 - 1));
            switch (sector)
            {
                case 0: fr = mx; fg = xx; fb = mn; break;
                case 1: fr = xx; fg = mx; fb = mn; break;
                case 2: fr = mn; fg = mx; fb = xx; break;
                case 3: fr = mn; fg = xx; fb = mx; break;
                case 4: fr = xx; fg = mn; fb = mx; break;
                default: fr = mx; fg = mn; fb = xx; break;
            }
        }

        float s = fc / mx;
        float sat = t.Sat[i0] + (t.Sat[i1] - t.Sat[i0]) * u;
        float f = sat * (1 + vib * (1 - s)) * baseSat;
        float l = LumaRf * fr + LumaGf * fg + LumaBf * fb;
        float lum = (t.Lum[i0] + (t.Lum[i1] - t.Lum[i0]) * u) * s;
        r = Clamp255((int)MathF.Round(l + (fr - l) * f + lum));
        g = Clamp255((int)MathF.Round(l + (fg - l) * f + lum));
        b = Clamp255((int)MathF.Round(l + (fb - l) * f + lum));
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
