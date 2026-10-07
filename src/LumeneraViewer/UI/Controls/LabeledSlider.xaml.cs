using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LumeneraViewer.UI.Controls;

/// <summary>Слайдер с подписью и числом. Двойной клик сбрасывает к <see cref="DefaultValue"/>.</summary>
public partial class LabeledSlider : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(LabeledSlider), new PropertyMetadata(""));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(LabeledSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((LabeledSlider)d).UpdateText()));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(LabeledSlider), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(LabeledSlider), new PropertyMetadata(100.0));

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(double), typeof(LabeledSlider), new PropertyMetadata(0.0));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(LabeledSlider), new PropertyMetadata(1.0));

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(string), typeof(LabeledSlider), new PropertyMetadata("0", (d, _) => ((LabeledSlider)d).UpdateText()));

    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(
        nameof(Suffix), typeof(string), typeof(LabeledSlider), new PropertyMetadata("", (d, _) => ((LabeledSlider)d).UpdateText()));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double DefaultValue { get => (double)GetValue(DefaultValueProperty); set => SetValue(DefaultValueProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    /// <summary>Формат числа, например "0", "0.00", "+0;−0;0".</summary>
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    public LabeledSlider()
    {
        InitializeComponent();
        UpdateText();
    }

    private void OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || !IsEnabled) return;
        Value = DefaultValue;
        e.Handled = true;
    }

    private void UpdateText()
    {
        if (ValueLabel is null) return;
        ValueLabel.Text = Value.ToString(Format, CultureInfo.CurrentCulture) + Suffix;
    }
}
