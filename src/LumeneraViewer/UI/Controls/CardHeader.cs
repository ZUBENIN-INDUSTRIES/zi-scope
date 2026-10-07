using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LumeneraViewer.UI.Effects;
using Wpf.Ui.Controls;

namespace LumeneraViewer.UI.Controls;

/// <summary>Заголовок карточки панели: иконка в цветном кружке + название.</summary>
public sealed class CardHeader : StackPanel
{
    private readonly SymbolIcon _icon = new() { FontSize = 16, Foreground = Brushes.White };
    private readonly System.Windows.Controls.TextBlock _title = new()
    {
        FontSize = 14.5,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10, 0, 0, 0),
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
        var badge = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(9),
            Background = new LinearGradientBrush(Palette.WithAlpha(Palette.Violet, 0.85), Palette.WithAlpha(Palette.Cyan, 0.85), 45),
            Child = _icon,
        };
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(badge);
        Children.Add(_title);
    }
}
