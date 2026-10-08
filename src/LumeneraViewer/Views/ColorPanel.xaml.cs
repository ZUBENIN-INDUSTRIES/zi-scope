using System.Windows.Controls;
using System.Windows.Input;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Views;

public partial class ColorPanel : UserControl
{
    public ColorPanel() => InitializeComponent();

    private void Profiles_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ColorPanelViewModel vm && vm.ApplyProfileCommand.CanExecute(null)) vm.ApplyProfileCommand.Execute(null);
    }
}
