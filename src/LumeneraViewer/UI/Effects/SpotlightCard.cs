using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// Стеклянная карточка (фон #14FFFFFF, рамка #22FFFFFF, радиус 14) с радиальным свечением акцента
/// (α 0.18, радиус 220 px), которое следует за курсором и гаснет за 250 мс при уходе мыши.
/// Содержимое — единственный дочерний элемент (Child).
/// </summary>
public class SpotlightCard : Decorator
{
    private static readonly Brush GlassFill = Palette.Frozen(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
    private static readonly Pen GlassPen = CreatePen(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    private Point _mouse;

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Thickness), typeof(SpotlightCard),
        new FrameworkPropertyMetadata(new Thickness(16), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(SpotlightCard),
        new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpotlightColorProperty = DependencyProperty.Register(
        nameof(SpotlightColor), typeof(Color), typeof(SpotlightCard),
        new FrameworkPropertyMetadata(Palette.Violet, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpotlightRadiusProperty = DependencyProperty.Register(
        nameof(SpotlightRadius), typeof(double), typeof(SpotlightCard),
        new FrameworkPropertyMetadata(220.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Текущая сила свечения 0..1 (анимируется).</summary>
    public static readonly DependencyProperty SpotlightOpacityProperty = DependencyProperty.Register(
        nameof(SpotlightOpacity), typeof(double), typeof(SpotlightCard),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public Color SpotlightColor { get => (Color)GetValue(SpotlightColorProperty); set => SetValue(SpotlightColorProperty, value); }
    public double SpotlightRadius { get => (double)GetValue(SpotlightRadiusProperty); set => SetValue(SpotlightRadiusProperty, value); }
    public double SpotlightOpacity { get => (double)GetValue(SpotlightOpacityProperty); set => SetValue(SpotlightOpacityProperty, value); }

    private static Pen CreatePen(Color c)
    {
        var p = new Pen(Palette.Frozen(c), 1);
        p.Freeze();
        return p;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var p = Padding;
        var inner = new Size(Math.Max(0, constraint.Width - p.Left - p.Right), Math.Max(0, constraint.Height - p.Top - p.Bottom));
        if (Child is null) return new Size(p.Left + p.Right, p.Top + p.Bottom);
        Child.Measure(inner);
        return new Size(Child.DesiredSize.Width + p.Left + p.Right, Child.DesiredSize.Height + p.Top + p.Bottom);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var p = Padding;
        Child?.Arrange(new Rect(p.Left, p.Top,
            Math.Max(0, arrangeSize.Width - p.Left - p.Right), Math.Max(0, arrangeSize.Height - p.Top - p.Bottom)));
        return arrangeSize;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _mouse = e.GetPosition(this);
        Fade(1, 150);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        Fade(0, 250);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.GetPosition(this);
        InvalidateVisual();
    }

    private void Fade(double to, int ms) =>
        BeginAnimation(SpotlightOpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)));

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(RenderSize);
        if (rect.Width <= 1 || rect.Height <= 1) return;
        var r = CornerRadius;
        var edgeRect = new Rect(0.5, 0.5, rect.Width - 1, rect.Height - 1);
        dc.DrawRoundedRectangle(GlassFill, null, rect, r, r);
        dc.DrawRoundedRectangle(null, GlassPen, edgeRect, r, r);

        var o = SpotlightOpacity;
        if (o <= 0.001) return;
        var c = SpotlightColor;
        dc.PushClip(new RectangleGeometry(rect, r, r));
        dc.DrawRectangle(Radial(c, 0.18 * o, SpotlightRadius), null, rect);
        dc.Pop();
        // Подсветка рамки рядом с курсором.
        dc.DrawRoundedRectangle(null, new Pen(Radial(c, 0.8 * o, SpotlightRadius * 0.8), 1), edgeRect, r, r);
    }

    private RadialGradientBrush Radial(Color c, double alpha, double radius) => new()
    {
        MappingMode = BrushMappingMode.Absolute,
        Center = _mouse,
        GradientOrigin = _mouse,
        RadiusX = radius,
        RadiusY = radius,
        GradientStops =
        {
            new GradientStop(Palette.WithAlpha(c, alpha), 0),
            new GradientStop(Palette.WithAlpha(c, 0), 1),
        },
    };
}
