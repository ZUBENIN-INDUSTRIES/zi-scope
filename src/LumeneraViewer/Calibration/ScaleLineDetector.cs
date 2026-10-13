using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LumeneraViewer.ImageCalibration;

/// <summary>Цвет масштабной линии, которую умеет искать детектор.</summary>
public enum LineColor { Blue, Yellow, White, Black, Red }

/// <summary>
/// Найденная масштабная линия. Координаты — пиксели картинки; A — левый конец, B — правый (на одной высоте),
/// LengthPx = B.X − A.X. TextRegionAbove / TextRegionBelow — где искать подпись: над линией и под ней (обрезано по картинке).
/// </summary>
public sealed record DetectedLine(Point A, Point B, double LengthPx, LineColor Color, Rect TextRegionAbove, Rect TextRegionBelow);

/// <summary>
/// Поиск масштабной линии на снимке: самый длинный горизонтальный отрезок сплошного цвета
/// (синий, жёлтый, белый, чёрный, красный) толщиной до ~20 px, контрастный к фону над и под ним.
/// Без UI и библиотек; пиксели BGR24, stride = width·3.
/// </summary>
public static class ScaleLineDetector
{
    private const int MaxGap = 2;          // допустимый разрыв прогона (шум, сжатие)
    private const int MaxThickness = 24;
    private const double MaxSameColorAround = 0.3;
    private const int MinWhiteLift = 20;   // белая линия ярче типичного окружения хотя бы на столько (яркость 0–255)

    /// <summary>Найти линию на картинке (любой формат WPF приводится к BGR24).</summary>
    public static DetectedLine? Detect(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (px, w, h) = ToBgr24(image);
        return Detect(px, w, h);
    }

    /// <summary>Пиксели BGR24 картинки (stride = w·3).</summary>
    public static (byte[] Pixels, int Width, int Height) ToBgr24(BitmapSource image)
    {
        var src = image.Format == PixelFormats.Bgr24 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
        int w = src.PixelWidth, h = src.PixelHeight;
        var px = new byte[w * h * 3];
        src.CopyPixels(px, w * 3, 0);
        return (px, w, h);
    }

    /// <summary>Найти линию в буфере BGR24; null — линии нет.</summary>
    public static DetectedLine? Detect(byte[] bgr, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(bgr);
        if (width <= 0 || height <= 0 || bgr.Length < width * height * 3)
            throw new ArgumentException("Размер буфера не совпадает с размером картинки.", nameof(bgr));

        var cls = Classify(bgr, width, height);
        var minLen = Math.Max(20, width / 40);
        Bar? best = null;

        foreach (var color in Enum.GetValues<LineColor>())
        {
            var code = (byte)(color + 1);
            var active = new List<Bar>();
            var done = new List<Bar>();
            for (var y = 0; y < height; y++)
            {
                var next = new List<Bar>();
                foreach (var (x0, x1) in Runs(cls, width, y, code, minLen))
                {
                    var bar = active.FirstOrDefault(b => Overlap(b, x0, x1) >= 0.8 * Math.Min(b.X1 - b.X0 + 1, x1 - x0 + 1));
                    if (bar is not null) { active.Remove(bar); bar.Add(x0, x1, y); }
                    else bar = new Bar(color, x0, x1, y);
                    next.Add(bar);
                }
                done.AddRange(active);   // не продолжились на этой строке — закончились
                active = next;
            }
            done.AddRange(active);

            foreach (var b in done)
            {
                if (b.Thickness > MaxThickness || b.Thickness > b.Length / 4.0) continue;
                if (b.X0 == 0 && b.X1 == width - 1) continue;   // во всю ширину — рамка/разделитель, не масштаб
                if (!Contrasts(cls, width, height, b, code)) continue;
                if (best is not null && b.Length <= best.Length) continue;
                if (color == LineColor.White && !BrighterThanSurroundings(bgr, width, height, b)) continue;
                best = b;
            }
        }

        return best is null ? null : ToResult(best, width, height);
    }

    private static DetectedLine ToResult(Bar b, int width, int height)
    {
        // Центр толщины, концы — по самым длинным строкам (края сглаживания короче).
        var y = b.Top + (b.Bottom - b.Top) / 2.0 + 0.5;
        var a = new Point(b.X0, y);
        var e = new Point(b.X1 + 1, y);
        var len = e.X - a.X;
        var textH = Math.Clamp(len * 0.5, 40, 400);
        var top = Math.Max(0, b.Top - textH);
        var left = Math.Max(0, a.X - len * 0.25);
        var right = Math.Min(width, e.X + len * 0.25);
        var bottom = Math.Min(height, b.Bottom + 1 + textH);
        var below = new Rect(left, b.Bottom + 1, right - left, Math.Max(0, bottom - (b.Bottom + 1)));
        return new DetectedLine(a, e, len, b.Color, new Rect(left, top, right - left, b.Top - top), below);
    }

    /// <summary>Код цвета каждого пикселя: 0 — ни один, иначе LineColor + 1.</summary>
    private static byte[] Classify(byte[] px, int w, int h)
    {
        var cls = new byte[w * h];
        Parallel.For(0, h, y =>
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                var i = (row + x) * 3;
                cls[row + x] = ClassOf(px[i + 2], px[i + 1], px[i]);
            }
        });
        return cls;
    }

    /// <summary>Отнести цвет к одному из пяти (или ни к одному).</summary>
    internal static byte ClassOf(int r, int g, int b)
    {
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        if (min >= 210 && max - min <= 45) return (byte)LineColor.White + 1;
        if (max <= 50 && max - min <= 35) return (byte)LineColor.Black + 1;
        if (r >= 140 && g >= 130 && Math.Min(r, g) - b >= 80) return (byte)LineColor.Yellow + 1;
        if (r >= 130 && r - Math.Max(g, b) >= 80) return (byte)LineColor.Red + 1;
        if (b >= 120 && b - Math.Max(r, g) >= 60) return (byte)LineColor.Blue + 1;
        return 0;
    }

    /// <summary>Горизонтальные прогоны цвета code длиной ≥ minLen (разрывы до MaxGap склеиваются).</summary>
    private static IEnumerable<(int X0, int X1)> Runs(byte[] cls, int w, int y, byte code, int minLen)
    {
        var row = y * w;
        var start = -1; var last = -1;
        for (var x = 0; x < w; x++)
        {
            if (cls[row + x] != code) continue;
            if (start >= 0 && x - last - 1 <= MaxGap) { last = x; continue; }
            if (start >= 0 && last - start + 1 >= minLen) yield return (start, last);
            start = last = x;
        }
        if (start >= 0 && last - start + 1 >= minLen) yield return (start, last);
    }

    private static int Overlap(Bar b, int x0, int x1) => Math.Min(b.X1, x1) - Math.Max(b.X0, x0) + 1;

    /// <summary>Над и под отрезком (через 2 px от края) — в основном не его цвет.</summary>
    private static bool Contrasts(byte[] cls, int w, int h, Bar b, byte code)
    {
        int above = b.Top - 2, below = b.Bottom + 2;
        if (above < 0 || below >= h) return false;
        return Share(above) <= MaxSameColorAround && Share(below) <= MaxSameColorAround;

        double Share(int y)
        {
            var same = 0;
            for (var x = b.X0; x <= b.X1; x++) if (cls[y * w + x] == code) same++;
            return same / (double)(b.X1 - b.X0 + 1);
        }
    }

    /// <summary>
    /// Белой линии мало быть «белой» (все каналы ≥ 210): на светлом поле так выглядит и сам фон, а щель фона
    /// между тёмными структурами даёт длинный «белый» отрезок. Поэтому белая линия должна быть заметно ярче
    /// типичного окружения — 75-го процентиля яркости окна вокруг неё (на светлом поле это сам фон).
    /// </summary>
    private static bool BrighterThanSurroundings(byte[] bgr, int w, int h, Bar b)
    {
        var line = 0L; var n = 0;
        for (var y = b.Top; y <= b.Bottom; y++)
            for (var x = b.X0; x <= b.X1; x++) { line += Luma(bgr, (y * w + x) * 3); n++; }
        var lineLuma = line / (double)n;

        int padX = b.Length / 2, padY = Math.Max(40, b.Length / 4);
        int x0 = Math.Max(0, b.X0 - padX), x1 = Math.Min(w - 1, b.X1 + padX);
        int y0 = Math.Max(0, b.Top - padY), y1 = Math.Min(h - 1, b.Bottom + padY);
        var hist = new int[256];
        var total = 0;
        for (var y = y0; y <= y1; y += 2)
            for (var x = x0; x <= x1; x += 2)
            {
                if (y >= b.Top && y <= b.Bottom && x >= b.X0 && x <= b.X1) continue;   // сама линия — не окружение
                hist[Luma(bgr, (y * w + x) * 3)]++;
                total++;
            }
        if (total == 0) return true;
        int p75 = 0, acc = 0;
        while (p75 < 255 && (acc += hist[p75]) < total * 0.75) p75++;
        return lineLuma - p75 >= MinWhiteLift;
    }

    private static int Luma(byte[] bgr, int i) => (bgr[i] * 29 + bgr[i + 1] * 150 + bgr[i + 2] * 77) >> 8;

    /// <summary>Отрезок из подряд идущих строк-прогонов.</summary>
    private sealed class Bar(LineColor color, int x0, int x1, int y)
    {
        public LineColor Color { get; } = color;
        public int X0 { get; private set; } = x0;
        public int X1 { get; private set; } = x1;
        public int Top { get; } = y;
        public int Bottom { get; private set; } = y;
        public int Thickness => Bottom - Top + 1;
        public int Length => X1 - X0 + 1;

        public void Add(int x0, int x1, int y)
        {
            // Концы — по самой длинной строке: сглаженные края короче и не должны укорачивать отрезок.
            if (x1 - x0 > X1 - X0) { X0 = x0; X1 = x1; }
            Bottom = y;
        }
    }
}
