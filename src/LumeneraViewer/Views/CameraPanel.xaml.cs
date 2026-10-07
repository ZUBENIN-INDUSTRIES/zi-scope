using System.Windows.Controls;
using System.Windows.Input;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Views;

public partial class CameraPanel : UserControl
{
    public CameraPanel() => InitializeComponent();

    // Двойной клик по слайдеру экспозиции — значение камеры по умолчанию
    private void ExposureSlider_PreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || DataContext is not CameraPanelViewModel vm) return;
        vm.ResetExposure();
        e.Handled = true;
    }
}
