using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LumeneraViewer.Settings;
using LumeneraViewer.UI.Effects;
using LumeneraViewer.ViewModels;
using Wpf.Ui.Controls;

namespace LumeneraViewer;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;
    private bool _fullScreen;
    private WindowState _stateBeforeFullScreen;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(this, new SettingsStore());
        _vm.AttachSnackbar(SnackbarPresenter);
        DataContext = _vm;

        var w = _vm.Settings.Window;
        Width = Math.Max(MinWidth, w.Width);
        Height = Math.Max(MinHeight, w.Height);
        if (w.Maximized) WindowState = WindowState.Maximized;

        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Entrance.Play(Cards.Children.OfType<UIElement>());
        await _vm.InitializeAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel) return;   // закрытие отложено (например, дописываются снимки) — окно закроется позже
        var w = _vm.Settings.Window;
        var bounds = WindowState == WindowState.Normal && !_fullScreen ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!bounds.IsEmpty) { w.Width = bounds.Width; w.Height = bounds.Height; }
        w.Maximized = WindowState == WindowState.Maximized && !_fullScreen;
        _vm.Shutdown();
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => Zoom.FitToView();
    private void ActualPixels_Click(object sender, RoutedEventArgs e) => Zoom.ActualPixels();

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var menu = MoreButton.ContextMenu!;
        menu.PlacementTarget = MoreButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>Горячие клавиши. Не срабатывают, пока фокус в поле ввода.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsTextInputFocused()) return;
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            case Key.Space when !ctrl:
                if (_vm.CapturePanel.CaptureCommand.CanExecute(null)) _vm.CapturePanel.CaptureCommand.Execute(null);
                break;
            case Key.F when !ctrl:
                _vm.IsFrozen = !_vm.IsFrozen;
                break;
            case Key.F11:
                ToggleFullScreen();
                break;
            case Key.Tab when !ctrl:
                _vm.IsPanelCollapsed = !_vm.IsPanelCollapsed;
                break;
            case Key.D0 or Key.NumPad0 when ctrl:
                Zoom.FitToView();
                break;
            case Key.D1 or Key.NumPad1 when ctrl:
                Zoom.ActualPixels();
                break;
            case Key.Escape:
                if (_vm.ActiveTool != PreviewTool.None) _vm.ActiveTool = PreviewTool.None;
                else if (_fullScreen) ToggleFullScreen();
                else return;
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private static bool IsTextInputFocused() => Keyboard.FocusedElement is TextBoxBase or System.Windows.Controls.PasswordBox
        || (Keyboard.FocusedElement is DependencyObject d && FindAncestor<ComboBox>(d) is { IsEditable: true })
        || Keyboard.FocusedElement is DependencyObject p && FindAncestor<ContentDialog>(p) is not null;

    private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
    {
        for (var cur = d; cur is not null; cur = System.Windows.Media.VisualTreeHelper.GetParent(cur) ?? LogicalTreeHelper.GetParent(cur))
            if (cur is T t) return t;
        return null;
    }

    private void ToggleFullScreen()
    {
        _fullScreen = !_fullScreen;
        if (_fullScreen)
        {
            _stateBeforeFullScreen = WindowState;
            TitleBar.Visibility = Visibility.Collapsed;
            WindowState = WindowState.Normal; // иначе развёрнутое окно не перекроет панель задач
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            TitleBar.Visibility = Visibility.Visible;
            ResizeMode = ResizeMode.CanResize;
            WindowState = _stateBeforeFullScreen;
        }
    }
}
