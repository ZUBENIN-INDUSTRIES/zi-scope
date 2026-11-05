using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using LumeneraViewer.UI;
using LumeneraViewer.ViewModels;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace LumeneraViewer.Install;

/// <summary>
/// Диалоги установки: предложение при запуске скачанного exe, пункт меню «Установить на компьютер…»,
/// удаление по <c>ZI-scope.exe --uninstall</c> (его запускает «Приложения» Windows).
/// </summary>
public static class InstallFlow
{
    private const string DeclinedKey = "install.declinedVersion";

    /// <summary>Куда ставить: обычно — профиль пользователя; с <c>--install-root</c> — внутрь указанной папки.</summary>
    public static InstallTargets Targets =>
        App.InstallRootOverride is { } root ? InstallTargets.UnderRoot(root) : InstallTargets.Default();

    /// <summary>Версия программы без хвоста сборки («0.1.0-alpha»).</summary>
    public static string Version
    {
        get
        {
            var v = typeof(InstallFlow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? typeof(InstallFlow).Assembly.GetName().Version?.ToString() ?? "0";
            var plus = v.IndexOf('+');
            return plus >= 0 ? v[..plus] : v;
        }
    }

    private static string Publisher =>
        typeof(InstallFlow).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ZI-scope";

    /// <summary>
    /// Установка имеет смысл: это опубликованный однофайловый exe (у сборки из исходников рядом лежит ZI-scope.dll)
    /// и запущен он не из папки установки.
    /// </summary>
    public static bool CanInstall =>
        Environment.ProcessPath is { } exe
        && !File.Exists(Path.Combine(AppContext.BaseDirectory, "ZI-scope.dll"))
        && !AppInstaller.IsRunningInstalled(Targets, exe);

    /// <summary>При запуске: предложить установить (или обновить) программу. true — установлена и перезапущена.</summary>
    public static async Task<bool> OfferAtStartupAsync(MainViewModel host)
    {
        if (!CanInstall) return false;
        var t = Targets;
        var installed = t.Registry.InstalledVersion();
        if (AppInstaller.IsInstalled(t) && installed == Version) return false;   // эта версия уже стоит
        if (host.Settings.Extensions.TryGetValue(DeclinedKey, out var e) && e.ValueKind == JsonValueKind.String && e.GetString() == Version)
            return false;

        var update = AppInstaller.IsInstalled(t);
        var choice = await Dialogs.ShowAsync(host.Owner, new ContentDialog
        {
            Title = update ? "Обновить установленную ZI-scope?" : "Установить ZI-scope на этот компьютер?",
            Content = Dialogs.Text(
                update
                    ? $"На компьютере стоит ZI-scope {installed ?? "другой версии"}. Заменить её этой версией ({Version})? Настройки и снимки сохранятся."
                    : "Программа скопируется в вашу папку программ, появится в меню «Пуск» и на рабочем столе, " +
                      "а удалить её можно будет через «Параметры → Приложения». Права администратора не нужны.",
                "Можно и не устанавливать — этот exe работает и сам по себе."),
            PrimaryButtonText = update ? "Обновить" : "Установить",
            PrimaryButtonAppearance = ControlAppearance.Primary,
            SecondaryButtonText = "Не спрашивать",
            CloseButtonText = "Не сейчас",
        }, 560);
        if (choice == ContentDialogResult.Secondary)
        {
            host.Settings.Extensions[DeclinedKey] = JsonSerializer.SerializeToElement(Version);
            host.SaveSettingsSoon();
            host.Notify("Установка", "Установить программу можно в любой момент: меню «⋯» → «Установить на компьютер…».", NotifyKind.Info);
            return false;
        }
        if (choice != ContentDialogResult.Primary) return false;
        return await InstallAsync(host);
    }

    /// <summary>Установить и перезапустить установленную копию. true — программа сейчас закроется.</summary>
    public static async Task<bool> InstallAsync(MainViewModel host)
    {
        if (Environment.ProcessPath is not { } exe) return false;
        var t = Targets;
        try
        {
            await Task.Run(() => AppInstaller.Install(t, exe, Version, Publisher, desktopShortcut: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            host.Notify("ZI-scope не установлена",
                "Не удалось записать программу: " + ex.Message + " Если установленная ZI-scope сейчас открыта — закройте её и повторите.",
                NotifyKind.Error);
            return false;
        }
        catch (Exception ex)
        {
            host.Notify("ZI-scope не установлена", ex.Message, NotifyKind.Error);
            return false;
        }

        host.SaveSettingsNow();
        try
        {
            var psi = new ProcessStartInfo(t.InstalledExe) { UseShellExecute = false, WorkingDirectory = t.InstallDir };
            foreach (var a in Environment.GetCommandLineArgs().Skip(1)) psi.ArgumentList.Add(a);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            host.Notify("ZI-scope установлена", "Запустите её из меню «Пуск». (" + ex.Message + ")", NotifyKind.Success);
            return false;
        }
        Application.Current.Shutdown();
        return true;
    }

    /// <summary><c>ZI-scope.exe --uninstall</c>: спросить, убрать ярлыки и запись, удалить папку после выхода.</summary>
    public static void RunUninstall()
    {
        var t = Targets;
        if (MessageBox.Show("Удалить ZI-scope с этого компьютера?\n\nНастройки программы и сделанные снимки останутся.",
                "Удаление ZI-scope", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            AppInstaller.Uninstall(t);
            if (Directory.Exists(t.InstallDir)) AppInstaller.ScheduleFolderRemoval(t.InstallDir, Environment.ProcessId);
            MessageBox.Show("ZI-scope удалена.", "Удаление ZI-scope", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось удалить: " + ex.Message, "Удаление ZI-scope", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
