using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Views;

/// <summary>Карта шагов серии; при смене текущего шага прокручивает к нему.</summary>
public partial class SeriesMap : UserControl
{
    private SeriesPanelViewModel? _vm;

    public SeriesMap()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as SeriesPanelViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        };
        IsVisibleChanged += (_, _) => { if (IsVisible) ScrollToCurrent(); };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SeriesPanelViewModel.CurrentItem)) ScrollToCurrent();
    }

    private void ScrollToCurrent()
    {
        if (_vm?.CurrentItem is not { } item) return;
        Dispatcher.BeginInvoke(() => { if (Map.Items.Contains(item)) Map.ScrollIntoView(item); }, DispatcherPriority.Background);
    }
}
