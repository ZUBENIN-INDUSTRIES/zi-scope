using System.Windows.Controls;
using System.Windows.Input;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Views;

public partial class ConfigProfilesView : UserControl
{
    public ConfigProfilesView() => InitializeComponent();

    private void List_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ConfigProfilesViewModel vm && vm.LoadCommand.CanExecute(null)) vm.LoadCommand.Execute(null);
    }
}
