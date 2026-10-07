using CommunityToolkit.Mvvm.ComponentModel;

namespace LumeneraViewer.ViewModels;

/// <summary>Заглушка слота (таск 06). Хост — MainViewModel.</summary>
public partial class GalleryViewModel : ObservableObject
{
    public MainViewModel Host { get; }

    public GalleryViewModel(MainViewModel host) => Host = host;
}
