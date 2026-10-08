using System.Windows.Controls;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Views;

public partial class MagnificationBar : UserControl
{
    public MagnificationBar()
    {
        InitializeComponent();
        // Снаружи DataContext — хост; переключателю нужна своя VM поверх него.
        // VM подписана на хост и живёт столько же, сколько он: на тот же хост новую не создаём.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is MainViewModel host && Root.DataContext is MagnificationBarViewModel vm && vm.Host == host) return;
            Root.DataContext = e.NewValue is MainViewModel h ? new MagnificationBarViewModel(h) : null;
        };
    }
}
