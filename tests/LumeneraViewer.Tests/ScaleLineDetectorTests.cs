using LumeneraViewer.ImageCalibration;

namespace LumeneraViewer.Tests;

/// <summary>Поиск масштабной линии на синтетических картинках: 5 цветов, светлый и тёмный фон, толщина 1–8 px, текст и шум.</summary>
public sealed class ScaleLineDetectorTests
{
    private const int W = 640, H = 480;

    private static readonly Dictionary<LineColor, (byte B, byte G, byte R)> Colors = new()
    {
        [LineColor.Blue] = (255, 60, 20),
        [LineColor.Yellow] = (0, 230, 255),
        [LineColor.White] = (255, 255, 255),
        [LineColor.Black] = (0, 0, 0),
        [LineColor.Red] = (20, 20, 235),
    };

    /// <summary>Фон «как препарат»: светлый или тёмный, с шумом и пятнами.</summary>
    private static byte[] Background(bool light, int seed)
    {
        var rnd = new Random(seed);
        var px = new byte[W * H * 3];
        var (b, g, r) = light ? (190, 196, 202) : (70, 62, 58);
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                // мягкие пятна (клетки) + шум ±12
                var blob = (int)(18 * Math.Sin(x / 37.0) * Math.Cos(y / 29.0));
                var i = (y * W + x) * 3;
                px[i] = Clamp(b + blob + rnd.Next(-12, 13));
                px[i + 1] = Clamp(g + blob + rnd.Next(-12, 13));
                px[i + 2] = Clamp(r + blob + rnd.Next(-12, 13));
            }
        return px;
    }

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

    private static void Fill(byte[] px, int x0, int y0, int w, int h, (byte B, byte G, byte R) c, int seed)
    {
        var rnd = new Random(seed);
        for (var y = y0; y < y0 + h; y++)
            for (var x = x0; x < x0 + w; x++)
            {
                var i = (y * W + x) * 3;
                px[i] = Clamp(c.B + rnd.Next(-8, 9));
                px[i + 1] = Clamp(c.G + rnd.Next(-8, 9));
                px[i + 2] = Clamp(c.R + rnd.Next(-8, 9));
            }
    }

    /// <summary>Подобие подписи «100 µm» над линией: штрихи 2–3 px высотой 22 px того же цвета.</summary>
    private static void Label(byte[] px, int x0, int baseY, (byte B, byte G, byte R) c)
    {
        for (var k = 0; k < 6; k++)
        {
            var gx = x0 + k * 16;
            Fill(px, gx, baseY - 22, 3, 22, c, k);          // вертикаль
            Fill(px, gx, baseY - 22, 11, 3, c, k + 10);     // верх
            Fill(px, gx, baseY - 3, 11, 3, c, k + 20);      // низ
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var color in Enum.GetValues<LineColor>())
            foreach (var light in new[] { true, false })
                foreach (var th in new[] { 1, 3, 8 })
                    // Белая на светлом и чёрная на тёмном фоне не контрастны — такую линию не наносят.
                    if (!(color == LineColor.White && light) && !(color == LineColor.Black && !light))
                        yield return new object[] { color, light, th };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Detect_FindsLineOfEachColorAndThickness(LineColor color, bool light, int thickness)
    {
        var px = Background(light, 7);
        const int x0 = 250, y0 = 400;
        Fill(px, x0, y0, 300, thickness, Colors[color], 99);
        Label(px, x0 + 100, y0 - 8, Colors[color]);

        var d = ScaleLineDetector.Detect(px, W, H);

        Assert.NotNull(d);
        Assert.Equal(color, d!.Color);
        Assert.InRange(d.LengthPx, 299, 301);
        Assert.InRange(d.A.X, 249, 251);
        Assert.InRange(d.B.X, 549, 551);
        Assert.InRange(d.A.Y, y0 - 1, y0 + thickness);
        Assert.Equal(d.A.Y, d.B.Y);
        // область текста — над линией и захватывает подпись
        Assert.True(d.TextRegionAbove.Bottom <= y0 + 1);
        Assert.True(d.TextRegionAbove.Top <= y0 - 30);
        Assert.True(d.TextRegionAbove.Left <= x0 + 100 && d.TextRegionAbove.Right >= x0 + 190);
    }

    [Theory]
    [InlineData(LineColor.Yellow, false)]
    [InlineData(LineColor.Blue, true)]
    [InlineData(LineColor.Black, true)]
    public void Detect_LabelBelowLine_RegionBelowCoversIt(LineColor color, bool light)
    {
        // Подпись под линией (линия у верха кадра): линия находится, нижняя область захватывает подпись.
        var px = Background(light, 11);
        const int x0 = 200, y0 = 60, th = 4;
        Fill(px, x0, y0, 300, th, Colors[color], 98);
        Label(px, x0 + 100, y0 + th + 8 + 22, Colors[color]);   // штрихи от y0+th+8 до y0+th+30

        var d = ScaleLineDetector.Detect(px, W, H);

        Assert.NotNull(d);
        Assert.Equal(color, d!.Color);
        Assert.InRange(d.LengthPx, 299, 301);
        Assert.True(d.TextRegionBelow.Top >= y0 + th - 1 && d.TextRegionBelow.Top <= y0 + th + 1);
        Assert.True(d.TextRegionBelow.Bottom >= y0 + th + 30);
        Assert.True(d.TextRegionBelow.Left <= x0 + 100 && d.TextRegionBelow.Right >= x0 + 190);
        // верхняя область упирается в край кадра и не залезает под линию
        Assert.True(d.TextRegionAbove.Top >= 0 && d.TextRegionAbove.Bottom <= y0 + 1);
    }

    [Fact]
    public void Detect_LineAtBottomEdge_RegionBelowClippedToImage()
    {
        var px = Background(true, 12);
        Fill(px, 150, H - 12, 300, 4, Colors[LineColor.Red], 97);   // 8 px до края, нижняя область (≥ 40 px) обрезается
        var d = ScaleLineDetector.Detect(px, W, H);
        Assert.NotNull(d);
        Assert.True(d!.TextRegionBelow.Bottom <= H);
        Assert.True(d.TextRegionBelow.Height >= 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Detect_NoLine_ReturnsNull(bool light)
    {
        var px = Background(light, 3);
        Label(px, 300, 200, light ? Colors[LineColor.Black] : Colors[LineColor.White]);   // только текст, без линии

        Assert.Null(ScaleLineDetector.Detect(px, W, H));
    }

    [Fact]
    public void Detect_LongestLineWins_OverShortMark()
    {
        var px = Background(true, 5);
        Fill(px, 50, 100, 120, 4, Colors[LineColor.Red], 1);
        Fill(px, 100, 300, 400, 4, Colors[LineColor.Blue], 2);

        var d = ScaleLineDetector.Detect(px, W, H);

        Assert.Equal(LineColor.Blue, d!.Color);
        Assert.InRange(d.LengthPx, 399, 401);
    }

    /// <summary>
    /// Светлое поле (все каналы ≥ 215, как у светлопольного препарата) с тёмными структурами, стоящими друг над другом:
    /// между ними остаются длинные тонкие полосы фона — по цвету «белые», но это не линия.
    /// </summary>
    private static byte[] BrightField(int seed, bool withStructures)
    {
        var rnd = new Random(seed);
        var px = new byte[W * H * 3];
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var blob = (int)(8 * Math.Sin(x / 41.0) * Math.Cos(y / 33.0));
                var i = (y * W + x) * 3;
                px[i] = Clamp(232 + blob + rnd.Next(-6, 7));
                px[i + 1] = Clamp(234 + blob + rnd.Next(-6, 7));
                px[i + 2] = Clamp(236 + blob + rnd.Next(-6, 7));
            }
        if (withStructures)
        {
            // Тёмные структуры, между которыми просвечивает фон: щели 3–5 px длиной 460–480 px
            // (длиннее масштабной линии), закрытые структурами со всех сторон.
            var dark = ((byte)70, (byte)40, (byte)90);
            var bg = ((byte)232, (byte)234, (byte)236);
            Fill(px, 40, 60, 520, 100, dark, seed + 1);
            Fill(px, 60, 90, 480, 4, bg, seed + 2);
            Fill(px, 60, 125, 470, 5, bg, seed + 3);
            Fill(px, 60, 200, 500, 55, dark, seed + 4);
            Fill(px, 80, 225, 460, 3, bg, seed + 5);
        }
        return px;
    }

    [Theory]
    [InlineData(LineColor.Blue)]
    [InlineData(LineColor.Yellow)]
    public void Detect_BrightFieldWithStructures_FindsColoredLine_NotBackgroundGap(LineColor color)
    {
        var px = BrightField(3, withStructures: true);
        Fill(px, 250, 400, 300, 4, Colors[color], 11);

        var d = ScaleLineDetector.Detect(px, W, H);

        Assert.NotNull(d);
        Assert.Equal(color, d!.Color);
        Assert.InRange(d.LengthPx, 299, 301);
    }

    [Fact]
    public void Detect_BrightFieldWithStructures_NoLine_ReturnsNull()
        => Assert.Null(ScaleLineDetector.Detect(BrightField(5, withStructures: true), W, H));

    [Fact]
    public void Detect_PlainBrightField_ReturnsNull()
        => Assert.Null(ScaleLineDetector.Detect(BrightField(6, withStructures: false), W, H));

    [Fact]
    public void Detect_WrongBufferSize_Throws()
        => Assert.Throws<ArgumentException>(() => ScaleLineDetector.Detect(new byte[10], W, H));
}
