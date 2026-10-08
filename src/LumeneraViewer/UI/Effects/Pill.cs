using System.Windows;
using System.Windows.Controls;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// «Таблетка» как у Gemini: радиус = половина высоты. Константа не подходит — WPF ужимает слишком большой
/// радиус по осям независимо, и широкая кнопка становится эллипсом. Поэтому радиус пересчитывается по размеру.
/// Работает на <see cref="Border"/>, кнопках WPF-UI (<c>CornerRadius</c>) и обычных кнопках (<c>Border.CornerRadius</c>).
/// </summary>
public static class Pill
{
    public static readonly DependencyProperty IsPillProperty = DependencyProperty.RegisterAttached(
        "IsPill", typeof(bool), typeof(Pill), new PropertyMetadata(false, OnChanged));

    public static bool GetIsPill(DependencyObject d) => (bool)d.GetValue(IsPillProperty);
    public static void SetIsPill(DependencyObject d, bool value) => d.SetValue(IsPillProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        fe.SizeChanged -= OnSizeChanged;
        if (e.NewValue is not true) return;
        fe.SizeChanged += OnSizeChanged;
        if (fe.ActualHeight > 0) Apply(fe);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged) Apply((FrameworkElement)sender);
    }

    private static void Apply(FrameworkElement fe)
    {
        var r = new CornerRadius(Math.Max(0, fe.ActualHeight / 2));
        switch (fe)
        {
            case Border b: b.CornerRadius = r; break;
            case Wpf.Ui.Controls.Button b: b.CornerRadius = r; break;
            default: fe.SetValue(Border.CornerRadiusProperty, r); break;
        }
    }
}
