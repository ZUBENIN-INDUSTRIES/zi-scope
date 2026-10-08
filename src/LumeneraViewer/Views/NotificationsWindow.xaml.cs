using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.ViewModels;
using Wpf.Ui.Controls;

namespace LumeneraViewer.Views;

/// <summary>
/// Небольшое немодальное окно с журналом уведомлений — вместо всплывающих сообщений поверх превью.
/// Одно на приложение, принадлежит главному окну; позиция, размер и «было открыто» —
/// в <c>Settings.Extensions["ui.notificationsWindow"]</c> (открытое при выходе откроется при следующем запуске).
/// </summary>
public partial class NotificationsWindow : FluentWindow
{
    private const string PlacementKey = "ui.notificationsWindow";
    private const int WmClose = 0x0010;

    private static NotificationsWindow? _instance;

    private readonly MainViewModel _host;
    private HwndSource? _ownerSource;
    private bool _ownerClosing;

    public IRelayCommand ClearCommand { get; }

    private NotificationsWindow(MainViewModel host)
    {
        _host = host;
        ClearCommand = new RelayCommand(host.Notifications.Clear);
        DataContext = host.Notifications;
        InitializeComponent();
        Owner = host.Owner;
        RestorePlacement();
        HookOwnerClose();
        IsVisibleChanged += (_, _) => UpdateViewing();
        StateChanged += (_, _) => UpdateViewing();
    }

    /// <summary>Открыть журнал или вывести на передний план уже открытый.</summary>
    public static void ShowFor(MainViewModel host)
    {
        if (_instance is { } w)
        {
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate();
            return;
        }
        _instance = new NotificationsWindow(host);
        _instance.Show();
        _instance.Activate();
    }

    /// <summary>При запуске: открыть журнал, если он был открыт при прошлом выходе.</summary>
    public static void RestoreIfWasOpen(MainViewModel host)
    {
        if (ReadPlacement(host) is { IsOpen: true }) ShowFor(host);
    }

    private void UpdateViewing() =>
        _host.Notifications.IsViewing = IsVisible && WindowState != WindowState.Minimized;

    /// <summary>
    /// Главное окно при закрытии сразу останавливает хост, и настройки после этого уже не пишутся.
    /// Хук HwndSource срабатывает раньше обработки WM_CLOSE самим окном — запоминаем позицию здесь.
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
        if (msg == WmClose)
        {
            _ownerClosing = true;   // закрываемся вместе с программой — журнал был открыт
            SavePlacement(isOpen: true);
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        _ownerSource?.RemoveHook(OwnerHook);
        _ownerSource = null;
        _host.Notifications.IsViewing = false;
        if (!_ownerClosing) SavePlacement(isOpen: false);
        if (ReferenceEquals(_instance, this)) _instance = null;
        base.OnClosed(e);
    }

    private sealed record Placement(double Left, double Top, double Width, double Height, bool IsOpen);

    private static Placement? ReadPlacement(MainViewModel host)
    {
        try
        {
            return host.Settings.Extensions.TryGetValue(PlacementKey, out var e) ? e.Deserialize<Placement>() : null;
        }
        catch (JsonException) { return null; }   // битая запись — как в первый раз
    }

    private void SavePlacement(bool isOpen)
    {
        var r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (r.IsEmpty) return;
        _host.Settings.Extensions[PlacementKey] =
            JsonSerializer.SerializeToElement(new Placement(r.Left, r.Top, r.Width, r.Height, isOpen));
        _host.SaveSettingsSoon();
    }

    private void RestorePlacement()
    {
        var p = ReadPlacement(_host);
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
        // Первый раз — в правом нижнем углу главного окна, чтобы не закрывать превью
        var o = Owner;
        if (o is null) { WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }
        Left = o.Left + Math.Max(0, o.ActualWidth - Width - 24);
        Top = o.Top + Math.Max(0, o.ActualHeight - Height - 24);
    }
}
