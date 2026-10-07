using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// Aurora: три размытых эллипса акцентных цветов (BlurEffect 120, α 0.35) медленно дрейфуют за окном.
/// Размытие кэшируется (BitmapCache), анимируются только TranslateTransform — CPU почти не тратится.
/// </summary>
public sealed class AuroraBackground : Grid
{
    private readonly List<(TranslateTransform t, double dx, double dy, double seconds)> _blobs = new();

    public AuroraBackground()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Background = Palette.Frozen(Palette.Background);

        AddBlob(Palette.Violet, 620, 460, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(-200, -180, 0, 0), 260, 160, 16);
        AddBlob(Palette.Cyan, 560, 420, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, -180, -200), -240, -140, 19);
        AddBlob(Palette.Pink, 460, 360, HorizontalAlignment.Center, VerticalAlignment.Center, new Thickness(260, 0, 0, 160), -200, 140, 13);

        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
    }

    private void AddBlob(Color c, double w, double h, HorizontalAlignment ha, VerticalAlignment va, Thickness margin, double dx, double dy, double seconds)
    {
        const double blur = 120;
        var ellipse = new Ellipse
        {
            Width = w,
            Height = h,
            Fill = Palette.Frozen(c),
            Opacity = 0.35,
            Effect = new BlurEffect { Radius = blur, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance },
        };
        // Контейнер с запасом под размытие; рендерится в битмап один раз, дальше только сдвигается.
        var holder = new Grid
        {
            Width = w + blur * 2,
            Height = h + blur * 2,
            HorizontalAlignment = ha,
            VerticalAlignment = va,
            Margin = margin,
            CacheMode = new BitmapCache(0.5),
        };
        holder.Children.Add(ellipse);
        var t = new TranslateTransform();
        holder.RenderTransform = t;
        Children.Add(holder);
        _blobs.Add((t, dx, dy, seconds));
    }

    private void Start()
    {
        foreach (var (t, dx, dy, s) in _blobs)
        {
            t.BeginAnimation(TranslateTransform.XProperty, Drift(dx, s));
            t.BeginAnimation(TranslateTransform.YProperty, Drift(dy, s * 0.77));
        }
    }

    private void Stop()
    {
        foreach (var (t, _, _, _) in _blobs)
        {
            t.BeginAnimation(TranslateTransform.XProperty, null);
            t.BeginAnimation(TranslateTransform.YProperty, null);
        }
    }

    private static DoubleAnimation Drift(double to, double seconds)
    {
        var a = new DoubleAnimation(0, to, TimeSpan.FromSeconds(Math.Max(12, seconds)))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Timeline.SetDesiredFrameRate(a, 30);
        return a;
    }
}
