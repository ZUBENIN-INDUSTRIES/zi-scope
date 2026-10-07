using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LumeneraViewer.UI;

/// <summary>
/// Общие диалоги (ContentDialog WPF-UI) для всего приложения.
/// Нет ContentDialogHost в окне — ждём его до ~2 с (при старте он регистрируется не сразу), потом
/// диалог считается отменённым: Confirm → false, AskText → null, Show → None. Исключений нет.
/// </summary>
public static class Dialogs
{
    public const double DefaultWidth = 480;
    private static readonly TimeSpan HostWait = TimeSpan.FromSeconds(2);

    /// <summary>Хост диалогов окна; null — его нет и за ~2 с не появился.</summary>
    public static async Task<ContentDialogHost?> GetHostAsync(Window owner)
    {
        var host = ContentDialogHost.GetForWindow(owner);
        for (var waited = TimeSpan.Zero; host is null && waited < HostWait; waited += TimeSpan.FromMilliseconds(50))
        {
            await Task.Delay(50);
            host = ContentDialogHost.GetForWindow(owner);
        }
        return host;
    }

    /// <summary>Показать собранный диалог. Нет хоста → None.</summary>
    public static async Task<ContentDialogResult> ShowAsync(Window owner, ContentDialog dialog, double maxWidth = DefaultWidth)
    {
        var host = await GetHostAsync(owner);
        if (host is null) return ContentDialogResult.None;
        dialog.DialogHostEx = host;
        dialog.DialogMaxWidth = maxWidth;
        return await dialog.ShowAsync();
    }

    /// <summary>Вопрос с подтверждением. true — нажата основная кнопка.</summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string text, string primary,
        string close = "Отмена", bool defaultIsClose = true)
        => await ShowAsync(owner, new ContentDialog
        {
            Title = title,
            Content = Text(text),
            PrimaryButtonText = primary,
            CloseButtonText = close,
            DefaultButton = defaultIsClose ? ContentDialogButton.Close : ContentDialogButton.Primary,
        }) == ContentDialogResult.Primary;

    /// <summary>Запрос строки. null — отмена или пустой ввод.</summary>
    public static async Task<string?> AskTextAsync(Window owner, string title, string label, string initial,
        string primary = "ОК", string? placeholder = null)
    {
        var box = new TextBox { Text = initial, MinWidth = 320, PlaceholderText = placeholder ?? "" };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Margin = new Thickness(0, 0, 0, 10) });
        panel.Children.Add(box);
        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primary,
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        if (await ShowAsync(owner, dialog) != ContentDialogResult.Primary) return null;
        var text = box.Text.Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Сообщение с одной кнопкой.</summary>
    public static Task InfoAsync(Window owner, string title, string text, string? secondary = null, string close = "Закрыть")
        => ShowAsync(owner, new ContentDialog { Title = title, Content = Text(text, secondary), CloseButtonText = close });

    /// <summary>Если в окне открыт диалог — закрыть его: хост показывает один диалог за раз.</summary>
    public static void CloseOpen(Window owner)
    {
        if (ContentDialogHost.GetForWindow(owner)?.Content is ContentDialog open) open.Hide();
    }

    /// <summary>Текст диалога: основной абзац и (необязательно) приглушённый второй.</summary>
    public static UIElement Text(string main, string? secondary = null)
    {
        var sp = new StackPanel { MaxWidth = 460 };
        sp.Children.Add(new TextBlock { Text = main, TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        if (!string.IsNullOrEmpty(secondary))
            sp.Children.Add(new TextBlock { Text = secondary, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Margin = new Thickness(0, 10, 0, 0) });
        return sp;
    }
}
