using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LumeneraViewer.Settings;
using LumeneraViewer.UI.Effects;

namespace LumeneraViewer.Overlays;

/// <summary>
/// Единый код рисования оверлеев — и для превью, и для впечатывания в файл.
/// Рисует в координатах кадра; scale умножает шрифты, толщины и отступы того, что попадает в файл
/// (плашка, измерения): и в превью, и в файле это frameWidth/1600 — превью показывает кадр таким, каким он
/// сохранится. Экранные направляющие (сетка, перекрестие, проводимая линия) — в guideScale (в превью 1/зум).
/// Результат зависит только от (state, frameSize, scale): pixelsPerDip фиксирован = 1.
/// </summary>
public static class OverlayRenderer
{
    private const double BackgroundAlpha = 0.55;
    private const double GridAlpha = 0.35;
    private const double CornerRadius = 6;
    private const double Padding = 8;
    private const double Margin = 16;
    private const double TextGap = 4;
    private const double MeasureFont = 13;
    private static readonly FontFamily Arial = new("Arial");
    private static readonly Typeface Face = new(Arial, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface FaceBold = new(Arial, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static Color ColorOf(OverlayColor c) => c switch
    {
        OverlayColor.Black => Colors.Black,
        OverlayColor.Yellow => Color.FromRgb(0xFA, 0xCC, 0x15),
        _ => Colors.White,
    };

    /// <summary>Подложка: белая для чёрной плашки, иначе чёрная; α 0.55.</summary>
    public static Color BackgroundOf(OverlayColor c) => Palette.WithAlpha(c == OverlayColor.Black ? Colors.White : Colors.Black, BackgroundAlpha);

    /// <summary>Цвета частей плашки: заданные явно или от общего цвета.</summary>
    public static (Color Text, Color Bar, Color Background) ScaleBarColors(OverlayState s)
    {
        var baseColor = ColorOf(s.Color);
        var bg = s.BackgroundColor ?? (s.Color == OverlayColor.Black ? Colors.White : Colors.Black);
        return (s.TextColor ?? baseColor, s.BarColor ?? baseColor, Palette.WithAlpha(bg, Math.Clamp(s.BackgroundOpacity, 0, 1)));
    }

    /// <summary>Коэффициент для впечатывания в файл: frameWidth / 1600.</summary>
    public static double ExportScale(int frameWidth) => Math.Max(1, frameWidth) / 1600.0;

    public static void Draw(DrawingContext dc, OverlayState s, Size frameSize, double scale, double? guideScale = null)
    {
        if (frameSize.Width <= 0 || frameSize.Height <= 0) return;
        if (!(scale > 0) || double.IsInfinity(scale)) scale = 1;
        var gs = guideScale is > 0 and var g && !double.IsInfinity(g) ? g : scale;

        dc.PushClip(new RectangleGeometry(new Rect(frameSize)));
        var color = ColorOf(s.Color);
        if (s.Grid) DrawGrid(dc, s, frameSize, gs, color);
        if (s.Crosshair) DrawCrosshair(dc, frameSize, gs, color);
        foreach (var m in s.Measurements) DrawMeasurement(dc, m, s, scale, color, dashed: false);
        if (s.PendingLine is { } p) DrawMeasurement(dc, p, s, gs, Palette.Cyan, dashed: true);
        if (s.ShowsScaleBar) DrawScaleBar(dc, s, frameSize, scale);
        dc.Pop();
    }

    /// <summary>Прямоугольник плашки в координатах кадра (для перетаскивания); null — плашка не рисуется.</summary>
    public static Rect? ScaleBarBounds(OverlayState s, Size frameSize, double scale)
        => Layout(s, frameSize, scale, Colors.White) is { } l ? l.Box : null;

    /// <summary>Коэффициент для превью плашки и измерений — тот же, что при записи в файл (что видно, то и сохранится).</summary>
    public static double PreviewScale(Size frameSize) => ExportScale((int)Math.Round(frameSize.Width));

    // ───── Плашка ─────

    private sealed record BarLayout(Rect Box, Rect Bar, FormattedText Text, Point TextOrigin);

    private static BarLayout? Layout(OverlayState s, Size frame, double scale, Color color)
    {
        if (!s.ShowsScaleBar || frame.Width <= 0 || frame.Height <= 0) return null;
        if (!(scale > 0) || double.IsInfinity(scale)) scale = 1;
        var (microns, barPx, _) = ScaleBarMath.Choose(s.MicronsPerPixel!.Value, (int)frame.Width, 0.2, s.FixedMicrons);
        var text = Text(ScaleBarMath.FormatLength(microns, s.Unit, s.UnitLanguage), Math.Clamp(s.FontSize, 6, 200) * scale, color, s.Bold ? FaceBold : Face);

        var pad = Padding * scale;
        var th = Math.Clamp(s.Thickness, 1, 50) * scale;
        var contentW = Math.Max(barPx, text.Width);
        var size = new Size(contentW + 2 * pad, pad + text.Height + TextGap * scale + th + pad);

        double x, y;
        if (s.CustomPosition is { } cp)
        {
            x = cp.X * frame.Width;
            y = cp.Y * frame.Height;
        }
        else
        {
            var m = Margin * scale;
            var right = s.Corner is ScaleBarCorner.BottomRight or ScaleBarCorner.TopRight;
            var bottom = s.Corner is ScaleBarCorner.BottomRight or ScaleBarCorner.BottomLeft;
            x = right ? frame.Width - m - size.Width : m;
            y = bottom ? frame.Height - m - size.Height : m;
        }
        x = Math.Clamp(x, 0, Math.Max(0, frame.Width - size.Width));
        y = Math.Clamp(y, 0, Math.Max(0, frame.Height - size.Height));

        var box = new Rect(new Point(x, y), size);
        var textOrigin = new Point(x + pad + (contentW - text.Width) / 2, y + pad);
        var bar = new Rect(x + pad + (contentW - barPx) / 2, y + pad + text.Height + TextGap * scale, barPx, th);
        return new BarLayout(box, bar, text, textOrigin);
    }

    private static void DrawScaleBar(DrawingContext dc, OverlayState s, Size frame, double scale)
    {
        var (text, bar, bg) = ScaleBarColors(s);
        if (Layout(s, frame, scale, text) is not { } l) return;
        if (s.Background && bg.A > 0)
            dc.DrawRoundedRectangle(Brush(bg), null, l.Box, CornerRadius * scale, CornerRadius * scale);
        dc.DrawText(l.Text, l.TextOrigin);
        dc.DrawRectangle(Brush(bar), null, l.Bar);
    }

    // ───── Измерения ─────

    private static void DrawMeasurement(DrawingContext dc, Measurement m, OverlayState s, double scale, Color color, bool dashed)
    {
        var len = m.LengthPixels;
        var pen = new Pen(Brush(color), 2 * scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (dashed) pen.DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0);
        pen.Freeze();
        // Полупрозрачная «обводка» под линией — чтобы линия читалась на любом фоне.
        var under = new Pen(Brush(BackgroundOf(s.Color)), 4 * scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        under.Freeze();

        dc.DrawLine(under, m.A, m.B);
        dc.DrawLine(pen, m.A, m.B);

        // Засечки на концах, перпендикулярно отрезку.
        var tick = 7 * scale;
        var n = len > 0.01 ? new Vector(-(m.B.Y - m.A.Y) / len, (m.B.X - m.A.X) / len) : new Vector(0, 1);
        foreach (var p in new[] { m.A, m.B })
        {
            dc.DrawLine(under, p - n * tick, p + n * tick);
            dc.DrawLine(pen, p - n * tick, p + n * tick);
        }
        if (len < 0.5) return;

        var text = Text(MeasurementText.Label(m, s.MicronsPerPixel, s.UnitLanguage), MeasureFont * scale, color);
        var mid = new Point((m.A.X + m.B.X) / 2, (m.A.Y + m.B.Y) / 2);
        // Подпись — сбоку от середины, со стороны нормали, смотрящей вверх.
        var side = n.Y > 0 ? -n : n;
        var off = 6 * scale + Math.Max(text.Width * Math.Abs(side.X), text.Height * Math.Abs(side.Y)) / 2;
        var c = mid + side * off;
        var pad = 4 * scale;
        var rect = new Rect(c.X - text.Width / 2 - pad, c.Y - text.Height / 2 - pad / 2, text.Width + 2 * pad, text.Height + pad);
        dc.DrawRoundedRectangle(Brush(BackgroundOf(s.Color)), null, rect, CornerRadius * scale, CornerRadius * scale);
        dc.DrawText(text, new Point(rect.X + pad, rect.Y + pad / 2));
    }

    // ───── Направляющие ─────

    private static void DrawCrosshair(DrawingContext dc, Size f, double scale, Color color)
    {
        var pen = new Pen(Brush(Palette.WithAlpha(color, 0.85)), 1.5 * scale);
        pen.Freeze();
        var cx = f.Width / 2;
        var cy = f.Height / 2;
        var gap = 10 * scale;
        dc.DrawLine(pen, new Point(0, cy), new Point(cx - gap, cy));
        dc.DrawLine(pen, new Point(cx + gap, cy), new Point(f.Width, cy));
        dc.DrawLine(pen, new Point(cx, 0), new Point(cx, cy - gap));
        dc.DrawLine(pen, new Point(cx, cy + gap), new Point(cx, f.Height));
        dc.DrawEllipse(null, pen, new Point(cx, cy), gap * 0.6, gap * 0.6);
    }

    /// <summary>Шаг сетки в пикселях кадра: мкм при калибровке, иначе % ширины.</summary>
    public static double GridStepPixels(OverlayState s, double frameWidth)
        => s.MicronsPerPixel is > 0 and var k
            ? s.GridStepMicrons / k
            : frameWidth * s.GridStepPercent / 100.0;

    private static void DrawGrid(DrawingContext dc, OverlayState s, Size f, double scale, Color color)
    {
        var step = GridStepPixels(s, f.Width);
        // Слишком частая сетка превращается в заливку и тысячи линий — не рисуем.
        if (!(step >= 2) || f.Width / step > 400 || f.Height / step > 400) return;
        var pen = new Pen(Brush(Palette.WithAlpha(color, GridAlpha)), 1 * scale);
        pen.Freeze();
        // Сетка от центра кадра — совпадает с перекрестием.
        var cx = f.Width / 2;
        var cy = f.Height / 2;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            for (var x = cx - Math.Floor(cx / step) * step; x <= f.Width; x += step)
            {
                g.BeginFigure(new Point(x, 0), false, false);
                g.LineTo(new Point(x, f.Height), true, false);
            }
            for (var y = cy - Math.Floor(cy / step) * step; y <= f.Height; y += step)
            {
                g.BeginFigure(new Point(0, y), false, false);
                g.LineTo(new Point(f.Width, y), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    // ───── Общее ─────

    private static FormattedText Text(string s, double size, Color color, Typeface? face = null)
        => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face ?? Face, Math.Max(1, size), Brush(color), 1.0);

    private static SolidColorBrush Brush(Color c) => Palette.Frozen(c);
}
