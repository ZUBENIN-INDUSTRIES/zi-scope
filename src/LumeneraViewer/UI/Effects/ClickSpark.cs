using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// ClickSpark: при клике 8 искр-линий разлетаются по кругу на 28 px и гаснут за 400 мс.
/// Подключается к любому элементу: <c>fx:ClickSpark.IsEnabled="True"</c>.
/// </summary>
public static class ClickSpark
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ClickSpark), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.RegisterAttached(
        "Color", typeof(Color), typeof(ClickSpark), new PropertyMetadata(Colors.White));

    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject o, bool v) => o.SetValue(IsEnabledProperty, v);
    public static Color GetColor(DependencyObject o) => (Color)o.GetValue(ColorProperty);
    public static void SetColor(DependencyObject o, Color v) => o.SetValue(ColorProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        el.PreviewMouseLeftButtonDown -= OnDown;
        if ((bool)e.NewValue) el.PreviewMouseLeftButtonDown += OnDown;
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        var el = (UIElement)sender;
        Burst(el, e.GetPosition(el));
    }

    /// <summary>Запустить вспышку в точке <paramref name="at"/> (координаты элемента).</summary>
    public static void Burst(UIElement el, Point at)
    {
        var layer = AdornerLayer.GetAdornerLayer(el);
        if (layer is null) return;
        var a = new SparkAdorner(el, at, GetColor(el));
        layer.Add(a);
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        anim.Completed += (_, _) => layer.Remove(a);
        a.BeginAnimation(SparkAdorner.ProgressProperty, anim);
    }

    private sealed class SparkAdorner : Adorner
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            "Progress", typeof(double), typeof(SparkAdorner),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly Point _at;
        private readonly Color _color;

        public SparkAdorner(UIElement el, Point at, Color color) : base(el)
        {
            _at = at;
            _color = color;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var p = (double)GetValue(ProgressProperty);
            const double distance = 28, length = 10;
            var pen = new Pen(new SolidColorBrush(Palette.WithAlpha(_color, 1 - p)), 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            for (var i = 0; i < 8; i++)
            {
                var ang = i * Math.PI / 4;
                var (cx, cy) = (Math.Cos(ang), Math.Sin(ang));
                var r0 = distance * p;
                var r1 = r0 + length * (1 - p);
                dc.DrawLine(pen, new Point(_at.X + cx * r0, _at.Y + cy * r0), new Point(_at.X + cx * r1, _at.Y + cy * r1));
            }
        }
    }
}
