using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Вкладка «Серия» и карта шагов. Пока заглушка: серию наполняет таск 06.
/// Контракт: <see cref="IsActive"/> — идёт ли серия; <see cref="CaptureStepCommand"/> — снять текущий шаг.
/// </summary>
public partial class SeriesPanelViewModel : ObservableObject
{
    public SeriesPanelViewModel(MainViewModel host)
    {
        Host = host;
        CaptureStepCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    public MainViewModel Host { get; }

    /// <summary>Идёт ли серия: тогда Пробел и главная кнопка снимают шаг, а внизу показывается карта серии.</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>Снять текущий шаг серии (заглушка ничего не делает).</summary>
    public IAsyncRelayCommand CaptureStepCommand { get; }
}
