using System.Windows;
using System.Windows.Controls;
using LumeneraViewer.UI.Effects;
using Wpf.Ui.Controls;

namespace LumeneraViewer.UI.Controls;

/// <summary>Заголовок раздела панели: приглушённая иконка + название (плоско, без плашек).</summary>
public sealed class CardHeader : StackPanel
{
    private readonly SymbolIcon _icon = new() { FontSize = 16, Foreground = Palette.TextSecondaryBrush };
    private readonly System.Windows.Controls.TextBlock _title = new()
    {
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = Palette.TextBrush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
    };

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CardHeader), new PropertyMetadata("", (d, e) => ((CardHeader)d)._title.Text = (string)e.NewValue));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(SymbolRegular), typeof(CardHeader), new PropertyMetadata(SymbolRegular.Empty, (d, e) => ((CardHeader)d)._icon.Symbol = (SymbolRegular)e.NewValue));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public SymbolRegular Icon { get => (SymbolRegular)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public CardHeader()
    {
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(0, 0, 0, 12);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_icon);
        Children.Add(_title);
    }
}
