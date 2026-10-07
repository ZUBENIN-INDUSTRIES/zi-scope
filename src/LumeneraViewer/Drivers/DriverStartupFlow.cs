using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LumeneraViewer.ViewModels;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace LumeneraViewer.Drivers;

/// <summary>Сценарий драйверов при старте и ручная переустановка. Всё — в UI-потоке.</summary>
public static class DriverStartupFlow
{
    /// <summary>true — драйверы есть, можно подключать реальную камеру.</summary>
    public static async Task<bool> EnsureDriversAsync(MainViewModel host)
    {
        var svc = new DriverService();
        if (svc.IsInstalled()) return true;
        if (host.Settings.Camera.PreferDemo) return false;

        var choice = await ShowAsync(host, new ContentDialog
        {
            Title = "Драйверы камеры не найдены",
            Content = Text(
                "Программа не нашла драйвер камеры Lumenera (lucamapi.dll), поэтому не может подключиться к микроскопу.",
                "Драйверы можно установить прямо сейчас из комплекта программы (папка Drivers рядом с программой). " +
                "Интернет не нужен — ничего не скачивается. Потребуются права администратора."),
            PrimaryButtonText = "Установить",
            PrimaryButtonIcon = new SymbolIcon(SymbolRegular.ArrowDownload24),
            PrimaryButtonAppearance = ControlAppearance.Primary,
            SecondaryButtonText = "Демо-режим",
            CloseButtonText = "Позже",
        });

        switch (choice)
        {
            case ContentDialogResult.Primary:
                return await InstallFlowAsync(host, svc);
            case ContentDialogResult.Secondary:
                host.StartDemo();
                return false;
            default:
                return false;
        }
    }

    /// <summary>Пункт меню «Установить/переустановить драйверы».</summary>
    public static async Task ReinstallAsync(MainViewModel host)
    {
        CloseOpenDialog(host);
        var svc = new DriverService();
        var installed = svc.IsInstalled();

        var choice = await ShowAsync(host, new ContentDialog
        {
            Title = installed ? "Переустановить драйверы из комплекта?" : "Установить драйверы из комплекта?",
            Content = Text(
                "Будет запущен установщик INFINITY ANALYZE/CAPTURE из папки Drivers рядом с программой. " +
                "Интернет не нужен. Потребуются права администратора.",
                installed ? "Во время установки камера будет отключена." : null),
            PrimaryButtonText = installed ? "Переустановить" : "Установить",
            PrimaryButtonAppearance = ControlAppearance.Primary,
            CloseButtonText = "Отмена",
        });
        if (choice != ContentDialogResult.Primary) return;

        if (await InstallFlowAsync(host, svc)) await host.ConnectAsync();
    }

    // ───── Общий поток установки ─────

    /// <summary>Тихая установка → при неудаче предложение обычного мастера. true — драйвер загружается.</summary>
    private static async Task<bool> InstallFlowAsync(MainViewModel host, DriverService svc)
    {
        if (svc.FindBundledInstaller() is null)
        {
            await ShowMissingInstallerAsync(host, svc);
            return false;
        }

        var result = await RunWithProgressAsync(host, svc, silent: true);
        if (result is DriverInstallResult.Failed or DriverInstallResult.TimedOut)
        {
            var ask = await ShowAsync(host, new ContentDialog
            {
                Title = "Запустить обычный мастер установки?",
                Content = Text(result == DriverInstallResult.TimedOut
                    ? "Тихая установка не завершилась за 10 минут. Можно запустить установщик в обычном режиме и пройти шаги мастера вручную."
                    : "Тихая установка не удалась. Можно запустить установщик в обычном режиме и пройти шаги мастера вручную."),
                PrimaryButtonText = "Запустить мастер",
                PrimaryButtonAppearance = ControlAppearance.Primary,
                CloseButtonText = "Отмена",
            });
            if (ask != ContentDialogResult.Primary)
            {
                host.Notify("Драйверы не установлены", "Установить их можно позже из меню «⋯».", NotifyKind.Warning);
                return false;
            }
            result = await RunWithProgressAsync(host, svc, silent: false);
        }

        switch (result)
        {
            case DriverInstallResult.Success:
                host.Notify("Драйверы установлены", "Подключаю камеру…", NotifyKind.Success);
                return true;
            case DriverInstallResult.SuccessRebootRequired:
                host.Notify("Драйверы установлены", "Рекомендуется перезагрузка компьютера.", NotifyKind.Warning);
                return true;
            case DriverInstallResult.Cancelled:
                host.Notify("Установка отменена", "Установить драйверы можно позже из меню «⋯».", NotifyKind.Info);
                return false;
            case DriverInstallResult.InstallerNotFound:
                await ShowMissingInstallerAsync(host, svc);
                return false;
            case DriverInstallResult.TimedOut:
                host.Notify("Установка не завершилась", "Установщик не ответил за 10 минут.", NotifyKind.Error);
                return false;
            default:
                host.Notify("Драйверы не установлены", "Установщик завершился с ошибкой или драйвер так и не загрузился.", NotifyKind.Error);
                return false;
        }
    }

    /// <summary>Окно с анимацией прогресса, пока идёт установщик. Закрыть его вручную нельзя.</summary>
    private static async Task<DriverInstallResult> RunWithProgressAsync(MainViewModel host, DriverService svc, bool silent)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new ProgressRing { IsIndeterminate = true, Width = 36, Height = 36, Margin = new Thickness(0, 0, 16, 0) });
        panel.Children.Add(Text(
            "Устанавливаю драйверы… это может занять несколько минут.",
            silent ? "Подтвердите запрос администратора, если он появится." : "Пройдите шаги мастера установки в его окне."));

        var dialog = new ContentDialog
        {
            Title = "Установка драйверов",
            Content = panel,
            IsFooterVisible = false,
        };
        var allowClose = false;
        dialog.Closing += (_, e) => { if (!allowClose) e.Cancel = true; };

        var shown = ShowAsync(host, dialog);
        DriverInstallResult result;
        try
        {
            result = await svc.InstallAsync(silent);
        }
        finally
        {
            allowClose = true;
            dialog.Hide();
            await shown;
        }
        return result;
    }

    /// <summary>История 7: установщика нет в комплекте — говорим, куда его положить.</summary>
    private static async Task ShowMissingInstallerAsync(MainViewModel host, DriverService svc)
    {
        var choice = await ShowAsync(host, new ContentDialog
        {
            Title = "Установщик драйверов не найден",
            Content = Text(
                "В папке Drivers рядом с программой нет установщика. Положите туда установщик INFINITY ANALYZE/CAPTURE.",
                svc.DriversFolder),
            PrimaryButtonText = "Открыть папку",
            PrimaryButtonIcon = new SymbolIcon(SymbolRegular.FolderOpen24),
            CloseButtonText = "Закрыть",
        });
        if (choice != ContentDialogResult.Primary) return;

        try
        {
            Directory.CreateDirectory(svc.DriversFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{svc.DriversFolder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            host.Notify("Не удалось открыть папку", ex.Message, NotifyKind.Error);
        }
    }

    // ───── Мелочи ─────

    private static async Task<ContentDialogResult> ShowAsync(MainViewModel host, ContentDialog dialog)
    {
        // При старте (Window.Loaded) хост диалогов может ещё не зарегистрироваться — ждём до ~2 с.
        var dialogHost = ContentDialogHost.GetForWindow(host.Owner);
        for (var i = 0; dialogHost is null && i < 40; i++)
        {
            await Task.Delay(50);
            dialogHost = ContentDialogHost.GetForWindow(host.Owner);
        }
        if (dialogHost is null) return ContentDialogResult.None;
        dialog.DialogHostEx = dialogHost;
        dialog.DialogMaxWidth = 560;
        return await dialog.ShowAsync();
    }

    /// <summary>Если открыт другой диалог (например, «Дополнительно»), закрыть его: хост показывает один диалог.</summary>
    private static void CloseOpenDialog(MainViewModel host)
    {
        if (ContentDialogHost.GetForWindow(host.Owner)?.Content is ContentDialog open) open.Hide();
    }

    private static UIElement Text(string main, string? secondary = null)
    {
        var sp = new StackPanel { MaxWidth = 460 };
        sp.Children.Add(new TextBlock { Text = main, TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        if (!string.IsNullOrEmpty(secondary))
            sp.Children.Add(new TextBlock { Text = secondary, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Margin = new Thickness(0, 10, 0, 0) });
        return sp;
    }
}
