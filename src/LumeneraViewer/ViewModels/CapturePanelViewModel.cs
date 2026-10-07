using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LumeneraViewer.ViewModels;

/// <summary>Заглушка слота (таск 06). CaptureCommand — кнопка «Снимок» и Пробел.</summary>
public partial class CapturePanelViewModel : ObservableObject
{
    public MainViewModel Host { get; }

    public CapturePanelViewModel(MainViewModel host) => Host = host;

    [RelayCommand]
    private void Capture() => Host.Notify("Снимок", "Сохранение ещё не подключено", NotifyKind.Info);
}
