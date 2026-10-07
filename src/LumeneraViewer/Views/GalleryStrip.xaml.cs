using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Views;

public partial class GalleryStrip : UserControl
{
    public GalleryStrip() => InitializeComponent();

    private GalleryViewModel? Vm => DataContext as GalleryViewModel;

    private static GalleryItem? ItemOf(object sender) => (sender as FrameworkElement)?.DataContext as GalleryItem;

    private void OnItemClick(object sender, MouseButtonEventArgs e) => Vm?.OpenCommand.Execute(ItemOf(sender));
    private void OnOpen(object sender, RoutedEventArgs e) => Vm?.OpenCommand.Execute(ItemOf(sender));
    private void OnShowInFolder(object sender, RoutedEventArgs e) => Vm?.ShowInFolderCommand.Execute(ItemOf(sender));
    private void OnDelete(object sender, RoutedEventArgs e) => Vm?.DeleteCommand.Execute(ItemOf(sender));

    /// <summary>Колесо мыши листает ленту по горизонтали.</summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
}
