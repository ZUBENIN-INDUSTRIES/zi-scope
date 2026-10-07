using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LumeneraViewer.UI.Effects;

/// <summary>Текст с бегущим бликом: градиентная заливка-маска сдвигается по тексту, цикл 3 с.</summary>
public sealed class ShinyText : TextBlock
{
    private readonly TranslateTransform _shift = new(-1, 0);

    public ShinyText()
    {
        var baseColor = Color.FromRgb(0xC9, 0xC4, 0xE8);
        Foreground = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            RelativeTransform = _shift,
            GradientStops =
            {
                new GradientStop(baseColor, 0),
                new GradientStop(baseColor, 0.38),
                new GradientStop(Colors.White, 0.5),
                new GradientStop(baseColor, 0.62),
                new GradientStop(baseColor, 1),
            },
        };
        Loaded += (_, _) =>
        {
            var a = new DoubleAnimation(-1, 1, TimeSpan.FromSeconds(3)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(a, 30);
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
        };
        Unloaded += (_, _) => _shift.BeginAnimation(TranslateTransform.XProperty, null);
    }
}
