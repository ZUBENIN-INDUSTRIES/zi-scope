using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LumeneraViewer.UI.Effects;

/// <summary>Появление: fade 0→1 и сдвиг Y 12→0 за 350 мс, со сдвигом 60 мс между элементами.</summary>
public static class Entrance
{
    public static void Play(IEnumerable<UIElement> items, double staggerMs = 60)
    {
        var i = 0;
        foreach (var el in items)
        {
            var begin = TimeSpan.FromMilliseconds(staggerMs * i++);
            var dur = TimeSpan.FromMilliseconds(350);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var t = new TranslateTransform(0, 12);
            el.RenderTransform = t;
            el.Opacity = 0;
            el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, dur) { BeginTime = begin, EasingFunction = ease });
            t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, dur) { BeginTime = begin, EasingFunction = ease });
        }
    }
}
