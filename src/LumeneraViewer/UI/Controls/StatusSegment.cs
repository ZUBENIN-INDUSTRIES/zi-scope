using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace LumeneraViewer.UI.Controls;

/// <summary>
/// Сегмент панели статуса в шапке: иконка (или свой индикатор) слева, подпись заглавными и значение.
/// Значение — Content; шаблон — неявный стиль в <c>UI/Effects/Effects.xaml</c>.
/// </summary>
public sealed class StatusSegment : ContentControl
{
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(StatusSegment), new PropertyMetadata("", (d, e) =>
            d.SetValue(CaptionTextPropertyKey, Track(((string?)e.NewValue ?? "").ToUpperInvariant()))));

    private static readonly DependencyPropertyKey CaptionTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CaptionText), typeof(string), typeof(StatusSegment), new PropertyMetadata(""));

    /// <summary>Подпись для шаблона: заглавными и с разрядкой (в WPF нет letter-spacing — вставляем волосяные пробелы).</summary>
    public static readonly DependencyProperty CaptionTextProperty = CaptionTextPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(SymbolRegular), typeof(StatusSegment), new PropertyMetadata(SymbolRegular.Empty));

    /// <summary>Если задан — показывается вместо иконки (например, пульсирующая точка состояния).</summary>
    public static readonly DependencyProperty IndicatorProperty = DependencyProperty.Register(
        nameof(Indicator), typeof(object), typeof(StatusSegment), new PropertyMetadata(null));

    /// <summary>Подпись над значением (выводится заглавными).</summary>
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string CaptionText => (string)GetValue(CaptionTextProperty);
    public SymbolRegular Icon { get => (SymbolRegular)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }

    private static string Track(string s) => string.Join(HairSpace, s.EnumerateRunes());
    private const string HairSpace = "\u200A";
}
