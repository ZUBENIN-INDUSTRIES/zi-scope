using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LumeneraViewer.Imaging;
using LumeneraViewer.UI.Effects;

namespace LumeneraViewer.UI.Controls;

/// <summary>
/// Редактор точечной тональной кривой: клик добавляет точку (до 12), перетаскивание двигает,
/// двойной клик по точке удаляет (крайние не удаляются). Фон — гистограмма канала и сетка.
/// Кривая рисуется той же интерполяцией, что <see cref="ToneCurve.BuildLut"/>.
/// </summary>
public sealed class ToneCurveEditor : FrameworkElement
{
    private const double Pad = 8;
    private const double HitRadius = 9;

    private static readonly Brush Back = Palette.InputBrush;
    private static readonly Pen GridPen = Frozen(new Pen(Palette.GridLineBrush, 1));
    private static readonly Pen DiagonalPen = Frozen(new Pen(Palette.Frozen(Palette.WithAlpha(Palette.Text, 0.19)), 1) { DashStyle = DashStyles.Dash });
    // Кисти и перья на канал (0 — общий, 1 — R, 2 — G, 3 — B) — создаются один раз
    private static readonly Brush[] PointFill = Palette.Channels.Select(c => (Brush)Palette.Frozen(c)).ToArray();
    private static readonly Brush[] HistFill = Palette.Channels.Select(c => (Brush)Palette.Frozen(Palette.WithAlpha(c, 0.19))).ToArray();
    private static readonly Pen[] CurvePen = Palette.Channels.Select(c => Frozen(new Pen(Palette.Frozen(c), 2))).ToArray();
    private static readonly Pen PointRing = Frozen(new Pen(Palette.InputBrush, 2));

    public static readonly DependencyProperty CurveProperty = DependencyProperty.Register(
        nameof(Curve), typeof(ToneCurve), typeof(ToneCurveEditor),
        new FrameworkPropertyMetadata(ToneCurve.Identity,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty HistogramProperty = DependencyProperty.Register(
        nameof(Histogram), typeof(Histogram), typeof(ToneCurveEditor),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0 — общий, 1 — R, 2 — G, 3 — B: цвет кривой и какая гистограмма фоном.</summary>
    public static readonly DependencyProperty ChannelProperty = DependencyProperty.Register(
        nameof(Channel), typeof(int), typeof(ToneCurveEditor),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public ToneCurve Curve { get => (ToneCurve)GetValue(CurveProperty); set => SetValue(CurveProperty, value); }
    public Histogram? Histogram { get => (Histogram?)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }
    public int Channel { get => (int)GetValue(ChannelProperty); set => SetValue(ChannelProperty, value); }

    private int _drag = -1;

    public ToneCurveEditor()
    {
        Focusable = false;
        Cursor = Cursors.Cross;
        ToolTip = "Клик — добавить точку (до 12), перетаскивание — двигать, двойной клик по точке — удалить";
    }

    private Rect Plot => new(Pad, Pad, Math.Max(1, ActualWidth - 2 * Pad), Math.Max(1, ActualHeight - 2 * Pad));

    private Point ToScreen(CurvePoint p)
    {
        var r = Plot;
        return new Point(r.X + p.X / 255 * r.Width, r.Y + (1 - p.Y / 255) * r.Height);
    }

    private CurvePoint ToCurve(Point s)
    {
        var r = Plot;
        return new CurvePoint(Math.Clamp((s.X - r.X) / r.Width * 255, 0, 255), Math.Clamp((1 - (s.Y - r.Y) / r.Height) * 255, 0, 255));
    }

    private List<CurvePoint> Points => CurveEditing.Sorted((Curve ?? ToneCurve.Identity).Points);

    private int HitTest(Point s)
    {
        var pts = Points;
        int best = -1;
        double bestD = HitRadius;
        for (int i = 0; i < pts.Count; i++)
        {
            double d = (ToScreen(pts[i]) - s).Length;
            if (d <= bestD) { bestD = d; best = i; }
        }
        return best;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var s = e.GetPosition(this);
        int hit = HitTest(s);
        if (e.ClickCount == 2)
        {
            if (hit >= 0 && CurveEditing.TryRemove(Points, hit) is { } removed) Curve = new ToneCurve(removed);
            _drag = -1;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        if (hit < 0 && CurveEditing.TryAdd(Points, ToCurve(s)) is { } added)
        {
            Curve = new ToneCurve(added.Points);
            hit = added.Index;
        }
        _drag = hit;
        if (_drag >= 0) CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag < 0 || e.LeftButton != MouseButtonState.Pressed) return;
        var moved = CurveEditing.Move(Points, _drag, ToCurve(e.GetPosition(this)));
        var curve = new ToneCurve(moved);
        if (curve != Curve) Curve = curve;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _drag = -1;
        ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _drag = -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        dc.DrawRoundedRectangle(Back, null, new Rect(0, 0, w, h), 6, 6);
        var r = Plot;
        int ch = Math.Clamp(Channel, 0, 3);

        // Гистограмма канала
        if (Histogram is { } hist)
        {
            var bins = ch switch { 1 => hist.R, 2 => hist.G, 3 => hist.B, _ => hist.Luma };
            double max = 1;
            for (int i = 1; i < 255; i++) max = Math.Max(max, bins[i]);
            max *= 1.05;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(r.Left, r.Bottom), true, true);
                for (int i = 0; i < bins.Length; i++)
                    ctx.LineTo(new Point(r.Left + r.Width * i / (bins.Length - 1), r.Bottom - Math.Min(1, bins[i] / max) * r.Height), true, false);
                ctx.LineTo(new Point(r.Right, r.Bottom), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(HistFill[ch], null, g);
        }

        // Сетка 4×4 и диагональ
        for (int i = 1; i < 4; i++)
        {
            double x = Math.Round(r.Left + r.Width * i / 4) + 0.5, y = Math.Round(r.Top + r.Height * i / 4) + 0.5;
            dc.DrawLine(GridPen, new Point(x, r.Top), new Point(x, r.Bottom));
            dc.DrawLine(GridPen, new Point(r.Left, y), new Point(r.Right, y));
        }
        dc.DrawLine(DiagonalPen, new Point(r.Left, r.Bottom), new Point(r.Right, r.Top));

        // Кривая — та же функция, что строит LUT
        var curve = Curve ?? ToneCurve.Identity;
        var f = curve.CreateEvaluator();
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            int steps = Math.Max(2, (int)r.Width);
            for (int i = 0; i <= steps; i++)
            {
                double x = 255.0 * i / steps;
                var p = ToScreen(new CurvePoint(x, Math.Clamp(f(x), 0, 255)));
                if (i == 0) ctx.BeginFigure(p, false, false);
                else ctx.LineTo(p, true, true);
            }
        }
        line.Freeze();
        dc.DrawGeometry(null, CurvePen[ch], line);

        // Точки
        foreach (var p in CurveEditing.Sorted(curve.Points))
            dc.DrawEllipse(PointFill[ch], PointRing, ToScreen(p), 5, 5);
    }

    private static Pen Frozen(Pen p)
    {
        p.Freeze();
        return p;
    }
}
