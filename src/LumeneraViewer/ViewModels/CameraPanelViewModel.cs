using CommunityToolkit.Mvvm.ComponentModel;

namespace LumeneraViewer.ViewModels;

/// <summary>Заглушка слота (таск 04). Хост — MainViewModel.</summary>
public partial class CameraPanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }

    public CameraPanelViewModel(MainViewModel host) => Host = host;
}
