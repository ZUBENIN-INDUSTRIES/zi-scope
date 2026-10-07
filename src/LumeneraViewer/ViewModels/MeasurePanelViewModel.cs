using CommunityToolkit.Mvvm.ComponentModel;

namespace LumeneraViewer.ViewModels;

/// <summary>Заглушка слота (таск 05). Хост — MainViewModel.</summary>
public partial class MeasurePanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }

    public MeasurePanelViewModel(MainViewModel host) => Host = host;
}
