using System.Windows;
using System.Windows.Media;
using LumeneraViewer.Imaging;
using LumeneraViewer.UI.Effects;

namespace LumeneraViewer.UI.Controls;

/// <summary>Лёгкая гистограмма: яркость и R/G/B полупрозрачными слоями (StreamGeometry, без визуального дерева).</summary>
public sealed class HistogramView : FrameworkElement
{
    private static readonly Brush Back = Palette.Frozen(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
    private static readonly Pen GridPen = Freeze(new Pen(Palette.Frozen(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)), 1));
    private static readonly Brush RFill = Palette.Frozen(Color.FromArgb(0x55, 0xF8, 0x71, 0x71));
    private static readonly Brush GFill = Palette.Frozen(Color.FromArgb(0x55, 0x4A, 0xDE, 0x80));
    private static readonly Brush BFill = Palette.Frozen(Color.FromArgb(0x55, 0x60, 0xA5, 0xFA));
    private static readonly Brush LFill = Palette.Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
    private static readonly Pen LPen = Freeze(new Pen(Palette.Frozen(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)), 1));

    public static readonly DependencyProperty HistogramProperty = DependencyProperty.Register(
        nameof(Histogram), typeof(Histogram), typeof(HistogramView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Histogram? Histogram { get => (Histogram?)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        dc.DrawRoundedRectangle(Back, null, new Rect(0, 0, w, h), 8, 8);
        for (int i = 1; i < 4; i++)
        {
            double x = Math.Round(w * i / 4) + 0.5;
            dc.DrawLine(GridPen, new Point(x, 4), new Point(x, h - 4));
        }

        if (Histogram is not { } hist) return;
        // Нормировка по максимуму без крайних корзин: пики на 0 и 255 иначе сплющивают всё остальное
        double max = 1;
        foreach (var ch in new[] { hist.R, hist.G, hist.B, hist.Luma })
            for (int i = 1; i < 255; i++) max = Math.Max(max, ch[i]);
        max *= 1.05;

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), 8, 8));
        dc.DrawGeometry(RFill, null, Area(hist.R, w, h, max, false));
        dc.DrawGeometry(GFill, null, Area(hist.G, w, h, max, false));
        dc.DrawGeometry(BFill, null, Area(hist.B, w, h, max, false));
        dc.DrawGeometry(LFill, null, Area(hist.Luma, w, h, max, false));
        dc.DrawGeometry(null, LPen, Area(hist.Luma, w, h, max, true));
        dc.Pop();
    }

    private static StreamGeometry Area(int[] bins, double w, double h, double max, bool lineOnly)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            double Y(int i) => h - 2 - Math.Min(1, bins[i] / max) * (h - 6);
            if (lineOnly) ctx.BeginFigure(new Point(0, Y(0)), false, false);
            else
            {
                ctx.BeginFigure(new Point(0, h), true, true);
                ctx.LineTo(new Point(0, Y(0)), false, false);
            }
            for (int i = 1; i < bins.Length; i++)
                ctx.LineTo(new Point(w * i / (bins.Length - 1), Y(i)), true, true);
            if (!lineOnly) ctx.LineTo(new Point(w, h), false, false);
        }
        g.Freeze();
        return g;
    }

    private static Pen Freeze(Pen p)
    {
        p.Freeze();
        return p;
    }
}

/// <summary>Полоса уровня 0…1 с градиентом палитры и маркером (например, максимум резкости).</summary>
public sealed class LevelBar : FrameworkElement
{
    private static readonly Brush Track = Palette.Frozen(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Fill = FreezeBrush(new LinearGradientBrush(Palette.Violet, Palette.Cyan, 0));
    private static readonly Brush MarkerBrush = Palette.Frozen(Palette.Pink);

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(LevelBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerProperty = DependencyProperty.Register(
        nameof(Marker), typeof(double), typeof(LevelBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Положение маркера 0…1; NaN — без маркера.</summary>
    public double Marker { get => (double)GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double r = h / 2;
        dc.DrawRoundedRectangle(Track, null, new Rect(0, 0, w, h), r, r);
        var v = double.IsFinite(Value) ? Math.Clamp(Value, 0, 1) : 0;
        if (v > 0) dc.DrawRoundedRectangle(Fill, null, new Rect(0, 0, Math.Max(h, w * v), h), r, r);
        if (double.IsFinite(Marker))
        {
            double x = Math.Clamp(Marker, 0, 1) * w;
            dc.DrawRoundedRectangle(MarkerBrush, null, new Rect(x - 1.5, -3, 3, h + 6), 1.5, 1.5);
        }
    }

    private static Brush FreezeBrush(Brush b)
    {
        b.Freeze();
        return b;
    }
}
