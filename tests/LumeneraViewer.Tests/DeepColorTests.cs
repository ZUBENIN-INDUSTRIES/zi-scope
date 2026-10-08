using System.IO;
using LumeneraViewer.Imaging;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class DeepColorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lv-deep-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // Кадр w×h одного цвета (B,G,R)
    private static byte[] Solid(byte b, byte g, byte r, int w = 4, int h = 2)
    {
        var px = new byte[w * h * 3];
        for (int i = 0; i < px.Length; i += 3) { px[i] = b; px[i + 1] = g; px[i + 2] = r; }
        return px;
    }

    private static byte[] Gray(byte v, int w = 4, int h = 2) => Solid(v, v, v, w, h);

    private static byte[] Run(byte[] src, DeepColor d, int w = 4, int h = 2)
    {
        var dst = new byte[src.Length];
        ImageProcessor.Apply(src, dst, w, h, ImageAdjustments.Default with { Deep = d });
        return dst;
    }

    private static byte[] Noise(int w, int h)
    {
        var px = new byte[w * h * 3];
        new Random(7).NextBytes(px);
        return px;
    }

    [Fact]
    public void Neutral_IsIdentity_AndKeepsEveryByte()
    {
        var fresh = new DeepColor { Hsl = Enumerable.Range(0, 8).Select(_ => new HslAdjust()).ToList() };
        Assert.True(fresh.IsNeutral);
        Assert.True((ImageAdjustments.Default with { Deep = fresh }).IsIdentity);
        // Радиус резкости без силы — тоже нейтраль
        Assert.True((DeepColor.Neutral with { SharpenRadius = 2.5 }).IsNeutral);
        Assert.False((ImageAdjustments.Default with { Deep = new DeepColor { Clarity = 10 } }).IsIdentity);

        var src = Noise(37, 23);
        Assert.Equal(src, Run(src, fresh, 37, 23));
    }

    [Fact]
    public void RedGain2_DoublesRedUntilClamp()
    {
        var d = new DeepColor { RedGain = 2 };
        var lo = Run(Solid(30, 40, 60), d);
        Assert.Equal(new byte[] { 30, 40, 120 }, lo[..3]);
        var hi = Run(Solid(30, 40, 200), d);
        Assert.Equal(new byte[] { 30, 40, 255 }, hi[..3]);
    }

    [Fact]
    public void MasterLevelsBlack50_Maps50ToZero_And255To255()
    {
        var d = new DeepColor { Master = new ChannelLevels(Black: 50) };
        Assert.Equal(0, Run(Gray(50), d)[0]);
        Assert.Equal(255, Run(Gray(255), d)[0]);
        // 152.5 — середина отрезка 50…255 → 127.5
        Assert.InRange((int)Run(Gray(152), d)[0], 126, 128);
    }

    [Fact]
    public void RedLevels_ChangeOnlyRed()
    {
        var d = new DeepColor { Red = new ChannelLevels(Black: 50) };
        var px = Run(Solid(50, 50, 50), d);
        Assert.Equal(new byte[] { 50, 50, 0 }, px[..3]);
    }

    [Fact]
    public void Curve_ThroughMidPoint_LiftsMidtones()
    {
        var curve = new ToneCurve([new(255, 255), new(128, 192), new(0, 0)]);   // порядок не важен
        var lut = curve.BuildLut();
        Assert.Equal(0, lut[0]);
        Assert.Equal(192, lut[128]);
        Assert.Equal(255, lut[255]);
        Assert.True(lut[64] > 64);
        for (int i = 1; i < 256; i++) Assert.True(lut[i] >= lut[i - 1], $"не монотонна в {i}");

        Assert.Equal(192, Run(Gray(128), new DeepColor { CurveMaster = curve })[0]);
        Assert.True(ToneCurve.Identity.IsIdentity);
        Assert.False(curve.IsIdentity);
    }

    [Fact]
    public void Curve_EndsAreClamped()
    {
        // Кривая начинается с (50,20): всё левее 50 — 20; правее 200 — 230
        var lut = new ToneCurve([new(50, 20), new(200, 230)]).BuildLut();
        Assert.Equal(20, lut[0]);
        Assert.Equal(20, lut[50]);
        Assert.Equal(230, lut[255]);
    }

    [Fact]
    public void HslBlueHuePlus100_MovesBlueTowardPurple_LeavesRed()
    {
        var d = DeepColor.Neutral.WithHsl(5, new HslAdjust(Hue: 100));
        // Чистый синий: оттенок 240° + 30° = 270° → R ≈ 0.5·255, G = 0, B = 255
        var blue = Run(Solid(255, 0, 0), d);
        Assert.InRange((int)blue[2], 115, 140);
        Assert.InRange((int)blue[1], 0, 5);
        Assert.InRange((int)blue[0], 245, 255);

        var red = Run(Solid(0, 0, 255), d);
        Assert.Equal(new byte[] { 0, 0, 255 }, red[..3]);
    }

    [Fact]
    public void HighlightsMinus100_DarkensBrights_KeepsDarks()
    {
        var d = new DeepColor { Highlights = -100 };
        var bright = Run(Gray(220), d);
        Assert.True(bright[0] <= 200, $"220 → {bright[0]}");
        Assert.Equal(bright[0], bright[1]);   // серый остаётся серым
        Assert.Equal(bright[1], bright[2]);
        Assert.InRange((int)Run(Gray(20), d)[0], 17, 20);
    }

    [Fact]
    public void ShadowsPlus100_LiftsDarks_KeepsBrights()
    {
        var d = new DeepColor { Shadows = 100 };
        var dark = Run(Gray(40), d);
        Assert.True(dark[0] >= 55, $"40 → {dark[0]}");
        Assert.Equal(dark[0], dark[2]);
        Assert.InRange((int)Run(Gray(235), d)[0], 235, 238);
    }

    [Fact]
    public void VibrancePlus100_BoostsMutedMoreThanSaturated()
    {
        // Пиксель 0 — малонасыщенный, пиксель 1 — насыщенный (B,G,R)
        var src = new byte[] { 120, 120, 140, 40, 40, 240 };
        var dst = new byte[6];
        ImageProcessor.Apply(src, dst, 2, 1, ImageAdjustments.Default with { Deep = new DeepColor { Vibrance = 100 } });
        double mutedGain = (dst[2] - dst[0]) / 20.0;    // размах R−B был 20
        double vividGain = (dst[5] - dst[3]) / 200.0;   // был 200
        Assert.True(mutedGain > 1.2, $"muted ×{mutedGain:F2}");
        Assert.True(mutedGain > vividGain, $"muted ×{mutedGain:F2}, vivid ×{vividGain:F2}");
    }

    [Fact]
    public void Sharpen_IncreasesStepContrast()
    {
        int w = 16, h = 8;
        var src = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = x < w / 2 ? (byte)100 : (byte)150;
                int i = (y * w + x) * 3;
                src[i] = src[i + 1] = src[i + 2] = v;
            }
        var dst = Run(src, new DeepColor { SharpenAmount = 100, SharpenRadius = 1 }, w, h);
        int row = 4 * w * 3;
        Assert.True(dst[row + (w / 2 - 1) * 3] < 100, $"тёмная сторона {dst[row + (w / 2 - 1) * 3]}");
        Assert.True(dst[row + (w / 2) * 3] > 150, $"светлая сторона {dst[row + (w / 2) * 3]}");
        Assert.Equal(100, dst[row]);        // вдали от края — без изменений
        Assert.Equal(150, dst[row + (w - 1) * 3]);
    }

    [Fact]
    public void Clarity_DoesNotChangeFlatFrame()
    {
        var src = Solid(90, 130, 170, 40, 30);
        Assert.Equal(src, Run(src, new DeepColor { Clarity = 100 }, 40, 30));
    }

    [Fact]
    public void Levels_BlackAtOrAboveWhite_DoNotThrow()
    {
        // Black = 255: всё ниже 255 → 0, само 255 → 255 (порог)
        var top = new DeepColor { Master = new ChannelLevels(Black: 255) };
        Assert.Equal(0, Run(Gray(254), top)[0]);
        Assert.Equal(255, Run(Gray(255), top)[0]);
        // White < Black: порог по точке чёрного 200
        var inv = new DeepColor { Red = new ChannelLevels(Black: 200, White: 100) };
        Assert.Equal(0, Run(Gray(150), inv)[2]);
        Assert.Equal(255, Run(Gray(220), inv)[2]);
    }

    [Fact]
    public void ToneCurve_NullOrEmptyPoints_BecomeIdentity()
    {
        var c = new ToneCurve([new(0, 0), new(128, 192), new(255, 255)]);
        var fromNull = c with { Points = null! };
        var fromEmpty = c with { Points = [] };
        Assert.True(fromNull.IsIdentity);
        Assert.True(fromEmpty.IsIdentity);
        Assert.Equal(128, fromNull.BuildLut()[128]);
        Assert.Equal(128, fromEmpty.BuildLut()[128]);
        Assert.True(new ToneCurve(null!).IsIdentity);
    }

    [Fact]
    public void ExposurePlus1_Is2xInLinearLight()
    {
        // (100/255)^2.2·2 в линейном свете → обратно: 100·2^(1/2.2) ≈ 137.0; −1 EV: 100/1.3703 ≈ 73.0
        Assert.InRange((int)Run(Gray(100), new DeepColor { ExposureEv = 1 })[0], 136, 138);
        Assert.InRange((int)Run(Gray(100), new DeepColor { ExposureEv = -1 })[0], 72, 74);
        // Кламп −3…+3: +10 EV даёт то же, что +3 (100·2^(3/2.2) ≈ 257 → 255), −10 — то же, что −3 (100/2.574 ≈ 38.9)
        Assert.Equal(Run(Gray(30), new DeepColor { ExposureEv = 3 }), Run(Gray(30), new DeepColor { ExposureEv = 10 }));
        Assert.InRange((int)Run(Gray(100), new DeepColor { ExposureEv = -10 })[0], 38, 40);
    }

    [Fact]
    public void WhitesPlus100_LiftsTopEnd_KeepsDarks()
    {
        // 200: L=0.784, 0.2·L⁴·255 ≈ 19.3 → ≈ 219; 50: 0.2·0.196⁴·255 ≈ 0.08 → 50
        var d = new DeepColor { Whites = 100 };
        Assert.InRange((int)Run(Gray(200), d)[0], 218, 220);
        Assert.Equal(50, Run(Gray(50), d)[0]);
    }

    [Fact]
    public void BlacksMinus100_CrushesBottomEnd_KeepsBrights()
    {
        // 50: 1−L=0.804, 0.2·0.804⁴·255 ≈ 21.3 → ≈ 29; 200: 0.2·0.216⁴·255 ≈ 0.11 → 200; 0 остаётся 0
        var d = new DeepColor { Blacks = -100 };
        Assert.InRange((int)Run(Gray(50), d)[0], 28, 30);
        Assert.Equal(200, Run(Gray(200), d)[0]);
        Assert.Equal(0, Run(Gray(0), d)[0]);
    }

    [Fact]
    public void HslYellowSaturationMinus100_IsSmoothAcrossSectorBorder()
    {
        var d = DeepColor.Neutral.WithHsl(2, new HslAdjust(Saturation: -100));
        // Оттенок ≈ 45° (R=255, G=191, B=0) — середина между оранжевым (30°) и жёлтым (60°):
        // оттенок 60·191/255 = 44.94°, u = 14.94/30 = 0.498, вес жёлтого u²(3−2u) ≈ 0.497 → множитель 0.503;
        // L = 0.2126·255 + 0.7152·191 ≈ 190.8 → R = 190.8 + 64.2·0.503 ≈ 223.1, B = 190.8·0.497 ≈ 94.8
        var mid = Run(Solid(0, 191, 255), d);
        Assert.InRange((int)mid[2], 221, 225);
        Assert.InRange((int)mid[0], 92, 97);
        // Чистый оранжевый (30°, G=128) почти не тронут, жёлтый (60°) — серый
        var orange = Run(Solid(0, 128, 255), d);
        Assert.Equal(255, orange[2]);
        Assert.InRange((int)orange[0], 0, 2);
        // Жёлтый: L = 0.2126·255 + 0.7152·255 ≈ 236.6, множитель ≈ 0.001 → почти серый ≈ 236–237
        var yellow = Run(Solid(0, 255, 255), d);
        Assert.InRange((int)yellow[0], 235, 237);
        Assert.InRange((int)yellow[2], 236, 238);

        // Без скачков: при шаге оттенка ≈ 0.24° синий канал меняется не больше чем на 5
        int prev = -1;
        for (int g = 100; g <= 255; g++)
        {
            int b = Run(Solid(0, (byte)g, 255), d)[0];
            if (prev >= 0) Assert.True(Math.Abs(b - prev) <= 5, $"скачок при G={g}: {prev} → {b}");
            prev = b;
        }
    }

    [Fact]
    public void ParallelApply_WithLocalOps_MatchesSequential()
    {
        int w = 160, h = 120;
        var a = ImageAdjustments.Default with { Deep = new DeepColor { Clarity = 60, SharpenAmount = 120, SharpenRadius = 2 } };
        var frames = Enumerable.Range(0, 8).Select(i => { var f = new byte[w * h * 3]; new Random(i).NextBytes(f); return f; }).ToArray();
        var expected = frames.Select(f => { var o = new byte[f.Length]; ImageProcessor.Apply(f, o, w, h, a); return o; }).ToArray();

        for (int round = 0; round < 5; round++)
        {
            var actual = frames.Select(f => new byte[f.Length]).ToArray();
            Parallel.For(0, frames.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                i => ImageProcessor.Apply(frames[i], actual[i], w, h, a));
            for (int i = 0; i < frames.Length; i++) Assert.Equal(expected[i], actual[i]);
        }
    }

    [Fact]
    public void SettingsStore_RoundTripsDeep()
    {
        var path = Path.Combine(_dir, "settings.json");
        var deep = new DeepColor
        {
            RedGain = 1.5, Master = new ChannelLevels(10, 240, 1.2), Blue = new ChannelLevels(Gamma: 0.8),
            ExposureEv = -0.5, Highlights = -40, Clarity = 25, SharpenAmount = 80, SharpenRadius = 1.5,
            CurveGreen = new ToneCurve([new(0, 0), new(100, 140), new(255, 255)]),
        }.WithHsl(3, new HslAdjust(10, -20, 30));
        var s = new AppSettings { Adjustments = ImageAdjustments.Default with { Contrast = 15, Deep = deep } };

        using (var store = new SettingsStore(path)) store.Save(s);
        using var store2 = new SettingsStore(path);
        var loaded = store2.Load();

        Assert.Equal(s.Adjustments, loaded.Adjustments);
        Assert.Equal(new HslAdjust(10, -20, 30), loaded.Adjustments.Deep.Hsl[3]);
        Assert.Equal(8, loaded.Adjustments.Deep.Hsl.Count);
    }

    [Fact]
    public void SettingsStore_OldJsonWithoutDeep_LoadsNeutral()
    {
        var path = Path.Combine(_dir, "settings.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, """{ "Adjustments": { "Brightness": 0, "Contrast": 0, "Gamma": 1, "Saturation": 100, "Temperature": 0, "Tint": 0, "Grayscale": false, "Negative": false } }""");
        using var store = new SettingsStore(path);
        var a = store.Load().Adjustments;
        Assert.True(a.Deep.IsNeutral);
        Assert.True(a.IsIdentity);
        Assert.Equal(8, a.Deep.Hsl.Count);
    }
}
