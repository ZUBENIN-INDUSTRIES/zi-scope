using CommunityToolkit.Mvvm.ComponentModel;

namespace LumeneraViewer.ViewModels;

/// <summary>Заглушка слота (таск 04). Хост — MainViewModel.</summary>
public partial class ColorPanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }

    public ColorPanelViewModel(MainViewModel host) => Host = host;
}
