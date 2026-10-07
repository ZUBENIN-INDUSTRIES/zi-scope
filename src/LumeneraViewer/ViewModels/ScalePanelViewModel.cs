using CommunityToolkit.Mvvm.ComponentModel;

namespace LumeneraViewer.ViewModels;

/// <summary>Заглушка слота (таск 05). Хост — MainViewModel.</summary>
public partial class ScalePanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }

    public ScalePanelViewModel(MainViewModel host) => Host = host;
}
