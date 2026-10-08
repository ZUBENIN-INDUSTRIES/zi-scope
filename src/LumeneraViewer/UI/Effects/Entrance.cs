using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LumeneraViewer.UI.Effects;

/// <summary>Сдержанное появление: прозрачность 0→1 и сдвиг Y 4→0 за 140 мс (не больше 150 мс по спецификации).</summary>
public static class Entrance
{
    public const int DurationMs = 140;

    public static void Play(UIElement el)
    {
        var dur = TimeSpan.FromMilliseconds(DurationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var t = new TranslateTransform(0, 4);
        el.RenderTransform = t;
        el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
        t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 0, dur) { EasingFunction = ease });
    }
}
