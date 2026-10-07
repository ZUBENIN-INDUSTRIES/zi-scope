using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LumeneraViewer.UI;
using LumeneraViewer.ViewModels;
using Wpf.Ui.Controls;

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

        var choice = await Dialogs.ShowAsync(host.Owner, new ContentDialog
        {
            Title = "Драйверы камеры не найдены",
            Content = Dialogs.Text(
                "Программа не нашла драйвер камеры Lumenera (lucamapi.dll), поэтому не может подключиться к микроскопу.",
                "Драйверы можно установить прямо сейчас из комплекта программы (папка Drivers рядом с программой). " +
                "Интернет не нужен — ничего не скачивается. Потребуются права администратора."),
            PrimaryButtonText = "Установить",
            PrimaryButtonIcon = new SymbolIcon(SymbolRegular.ArrowDownload24),
            PrimaryButtonAppearance = ControlAppearance.Primary,
            SecondaryButtonText = "Демо-режим",
            CloseButtonText = "Позже",
        }, 560);

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

    /// <summary>
    /// Пункт меню «Установить/переустановить драйверы». Перед запуском установщика открытая камера закрывается,
    /// после установки (успешной или нет) — переподключение: реальная камера или прежний демо-режим.
    /// </summary>
    public static async Task ReinstallAsync(MainViewModel host)
    {
        Dialogs.CloseOpen(host.Owner);
        var svc = new DriverService();
        var installed = svc.IsInstalled();
        var hasCamera = host.Camera is not null;

        var choice = await Dialogs.ShowAsync(host.Owner, new ContentDialog
        {
            Title = installed ? "Переустановить драйверы из комплекта?" : "Установить драйверы из комплекта?",
            Content = Dialogs.Text(
                "Будет запущен установщик INFINITY ANALYZE/CAPTURE из папки Drivers рядом с программой. " +
                "Интернет не нужен. Потребуются права администратора.",
                hasCamera ? "На время установки камера будет отключена, после — подключится снова." : null),
            PrimaryButtonText = installed ? "Переустановить" : "Установить",
            PrimaryButtonAppearance = ControlAppearance.Primary,
            CloseButtonText = "Отмена",
        }, 560);
        if (choice != ContentDialogResult.Primary) return;

        if (svc.FindBundledInstaller() is null)
        {
            await ShowMissingInstallerAsync(host, svc);
            return;
        }

        var wasDemo = await host.DisconnectForInstallAsync();
        var ok = false;
        try
        {
            ok = await InstallFlowAsync(host, svc);
        }
        finally
        {
            // Переподключение в любом случае; ConnectAsync/StartDemo снова включают автопоиск камеры.
            if (wasDemo && !ok) host.StartDemo();
            else await host.ConnectAsync();
        }
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
            var ask = await Dialogs.ConfirmAsync(host.Owner, "Запустить обычный мастер установки?",
                result == DriverInstallResult.TimedOut
                    ? "Тихая установка не завершилась за 10 минут и была прервана. Можно запустить установщик в обычном режиме и пройти шаги мастера вручную."
                    : "Тихая установка не удалась. Можно запустить установщик в обычном режиме и пройти шаги мастера вручную.",
                "Запустить мастер", defaultIsClose: false);
            if (!ask)
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
            case DriverInstallResult.TimedOutStillRunning:
                // Мастер не предлагаем: второй установщик поверх живого первого только навредит.
                await Dialogs.InfoAsync(host.Owner, "Установщик всё ещё работает",
                    "Установка не завершилась за 10 минут, и программа не смогла остановить установщик (он запущен с правами администратора).",
                    "Дождитесь, пока он закончит, или закройте его окно (при необходимости — через Диспетчер задач), " +
                    "затем повторите установку из меню «⋯».");
                return false;
            case DriverInstallResult.TimedOut:
                host.Notify("Установка не завершилась", "Установщик не ответил за 10 минут и был остановлен.", NotifyKind.Error);
                return false;
            default:
                host.Notify("Драйверы не установлены", "Установщик завершился с ошибкой или драйвер так и не загрузился.", NotifyKind.Error);
                return false;
        }
    }

    /// <summary>
    /// Окно с анимацией прогресса, пока идёт установщик. Закрыть его вручную нельзя.
    /// Диалог показывается ДО запуска установщика, поэтому Hide в конце всегда его закрывает:
    /// прогресс не может остаться на экране без кнопок.
    /// </summary>
    private static async Task<DriverInstallResult> RunWithProgressAsync(MainViewModel host, DriverService svc, bool silent)
    {
        var dialogHost = await Dialogs.GetHostAsync(host.Owner);
        if (dialogHost is null) return await svc.InstallAsync(silent);   // некуда показать — просто ставим

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new ProgressRing { IsIndeterminate = true, Width = 36, Height = 36, Margin = new Thickness(0, 0, 16, 0) });
        panel.Children.Add(Dialogs.Text(
            "Устанавливаю драйверы… это может занять несколько минут.",
            silent ? "Подтвердите запрос администратора, если он появится." : "Пройдите шаги мастера установки в его окне."));

        var dialog = new ContentDialog(dialogHost)
        {
            Title = "Установка драйверов",
            Content = panel,
            IsFooterVisible = false,
            DialogMaxWidth = 560,
        };
        var finished = false;
        dialog.Closing += (_, e) => { if (!finished) e.Cancel = true; };
        dialog.Loaded += (_, _) => { if (finished) dialog.Hide(); };   // на случай, если отрисовка запоздала

        var shown = dialog.ShowAsync();   // синхронно встаёт в хост до запуска установщика
        try
        {
            return await svc.InstallAsync(silent);
        }
        finally
        {
            finished = true;
            dialog.Hide();
            await shown;
        }
    }

    /// <summary>История 7: установщика нет в комплекте — говорим, куда его положить.</summary>
    private static async Task ShowMissingInstallerAsync(MainViewModel host, DriverService svc)
    {
        var choice = await Dialogs.ShowAsync(host.Owner, new ContentDialog
        {
            Title = "Установщик драйверов не найден",
            Content = Dialogs.Text(
                "В папке Drivers рядом с программой нет установщика. Положите туда установщик INFINITY ANALYZE/CAPTURE.",
                svc.DriversFolder),
            PrimaryButtonText = "Открыть папку",
            PrimaryButtonIcon = new SymbolIcon(SymbolRegular.FolderOpen24),
            CloseButtonText = "Закрыть",
        }, 560);
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
}
