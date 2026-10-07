using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// Кнопка с градиентом фиолетовый→бирюзовый и внешним свечением (BlurRadius 24);
/// при наведении свечение ×1.5 за 200 мс, при нажатии масштаб 0.96, при клике — ClickSpark.
/// Шаблон — в UI/Effects/Effects.xaml.
/// </summary>
public sealed class GlowButton : Button
{
    private const double Glow = 24;
    private readonly DropShadowEffect _glow = new() { Color = Palette.Violet, BlurRadius = Glow, ShadowDepth = 0, Opacity = 0.75 };
    private readonly ScaleTransform _scale = new(1, 1);

    public GlowButton()
    {
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _scale;
        ClickSpark.SetIsEnabled(this, true);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Surface") is UIElement surface) surface.Effect = _glow;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        AnimateGlow(Glow * 1.5, 1);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        AnimateGlow(Glow, 0.75);
        AnimateScale(1);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        AnimateScale(0.96);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        AnimateScale(1);
    }

    private void AnimateGlow(double radius, double opacity)
    {
        var d = TimeSpan.FromMilliseconds(200);
        _glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(radius, d));
        _glow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(opacity, d));
    }

    private void AnimateScale(double s)
    {
        var a = new DoubleAnimation(s, TimeSpan.FromMilliseconds(90));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
