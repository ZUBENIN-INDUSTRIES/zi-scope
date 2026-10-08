using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.ViewModels;
using Wpf.Ui.Controls;

namespace LumeneraViewer.Views;

/// <summary>
/// Немодальное окно «Глубокие настройки цвета»: одно на приложение, принадлежит главному окну,
/// позиция и размер — в <c>Settings.Extensions["ui.deepColorWindow"]</c>.
/// </summary>
public partial class DeepColorWindow : FluentWindow
{
    private const string PlacementKey = "ui.deepColorWindow";

    private static DeepColorWindow? _instance;

    /// <summary>Открыть окно или активировать уже открытое. Параметр — MainViewModel или ColorPanelViewModel.</summary>
    public static IRelayCommand<object?> OpenCommand { get; } = new RelayCommand<object?>(p =>
    {
        var host = p as MainViewModel ?? (p as ColorPanelViewModel)?.Host;
        if (host is not null) ShowFor(host);
    });

    private readonly MainViewModel _host;
    private readonly DeepColorViewModel _vm;

    private DeepColorWindow(MainViewModel host)
    {
        _host = host;
        _vm = new DeepColorViewModel(host);
        DataContext = _vm;
        InitializeComponent();
        Owner = host.Owner;
        RestorePlacement();
        HookOwnerClose();
    }

    private const int WmClose = 0x0010;
    private HwndSource? _ownerSource;

    /// <summary>
    /// При выходе из программы главное окно в своём Closing сразу останавливает хост, и после этого
    /// настройки уже не сохраняются. Хуки HwndSource вызываются раньше обработки WM_CLOSE самим окном
    /// (последний добавленный — первым), поэтому позицию и несброшенное изменение пишем здесь.
    /// </summary>
    private void HookOwnerClose()
    {
        if (Owner is null) return;
        var h = new WindowInteropHelper(Owner).Handle;
        _ownerSource = h == IntPtr.Zero ? null : HwndSource.FromHwnd(h);
        _ownerSource?.AddHook(OwnerHook);
    }

    private IntPtr OwnerHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClose) SaveState();   // закрытие могут отменить — сохранить лишний раз не вредно
        return IntPtr.Zero;
    }

    private void SaveState()
    {
        _vm.Flush();
        SavePlacement();
    }

    public static void ShowFor(MainViewModel host)
    {
        if (_instance is { } w)
        {
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate();
            return;
        }
        _instance = new DeepColorWindow(host);
        _instance.Show();
    }

    protected override void OnClosed(EventArgs e)
    {
        _ownerSource?.RemoveHook(OwnerHook);
        _ownerSource = null;
        SaveState();
        _vm.Dispose();
        if (ReferenceEquals(_instance, this)) _instance = null;
        base.OnClosed(e);
    }

    private sealed record Placement(double Left, double Top, double Width, double Height);

    private void RestorePlacement()
    {
        Placement? p = null;
        try
        {
            if (_host.Settings.Extensions.TryGetValue(PlacementKey, out var e)) p = e.Deserialize<Placement>();
        }
        catch (JsonException) { /* битая запись — как в первый раз */ }

        // Окно должно попасть на рабочий стол (монитор могли отключить)
        var desk = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (p is not null && p.Width >= MinWidth && p.Height >= MinHeight
            && desk.IntersectsWith(new Rect(p.Left + 40, p.Top, Math.Max(1, p.Width - 80), 40)))
        {
            Left = p.Left;
            Top = p.Top;
            Width = p.Width;
            Height = p.Height;
            return;
        }
        // Первый раз — у правого края главного окна
        var o = Owner;
        if (o is null) { WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }
        Height = Math.Min(Height, Math.Max(MinHeight, o.ActualHeight - 80));
        Left = o.Left + Math.Max(0, o.ActualWidth - Width - 380);
        Top = o.Top + 60;
    }

    private void SavePlacement()
    {
        var r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (r.IsEmpty || !double.IsFinite(r.Left)) return;
        _host.Settings.Extensions[PlacementKey] = JsonSerializer.SerializeToElement(new Placement(r.Left, r.Top, r.Width, r.Height));
        _host.SaveSettingsSoon();
    }
}
