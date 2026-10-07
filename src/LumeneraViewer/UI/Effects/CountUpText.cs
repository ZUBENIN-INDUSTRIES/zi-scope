using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace LumeneraViewer.UI.Effects;

/// <summary>Число, которое за 400 мс (CubicEase) «доезжает» до нового значения. NaN — прочерк.</summary>
public sealed class CountUpText : TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CountUpText), new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(string), typeof(CountUpText), new PropertyMetadata("0", (d, _) => ((CountUpText)d).UpdateText()));

    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(double), typeof(CountUpText), new PropertyMetadata(0.0, (d, _) => ((CountUpText)d).UpdateText()));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Формат числа, например "0.0" или "0".</summary>
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public double DisplayValue { get => (double)GetValue(DisplayValueProperty); set => SetValue(DisplayValueProperty, value); }

    public CountUpText() => UpdateText();

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var t = (CountUpText)d;
        var to = (double)e.NewValue;
        if (!double.IsFinite(to))
        {
            t.BeginAnimation(DisplayValueProperty, null);
            t.DisplayValue = double.NaN;
            return;
        }
        var from = t.DisplayValue;
        if (!double.IsFinite(from)) from = to;
        t.BeginAnimation(DisplayValueProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private void UpdateText() =>
        Text = double.IsFinite(DisplayValue) ? DisplayValue.ToString(Format, CultureInfo.CurrentCulture) : "—";
}
