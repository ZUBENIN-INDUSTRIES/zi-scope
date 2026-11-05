using System.Windows;
using System.Windows.Threading;
using LumeneraViewer.UI.Effects;

namespace LumeneraViewer;

public partial class App : Application
{
    /// <summary>
    /// Файл настроек из командной строки (<c>--settings путь</c>) — для проверок, чтобы не трогать
    /// настройки пользователя в %AppData%; null — обычный путь.
    /// </summary>
    public static string? SettingsPathOverride { get; private set; }

    /// <summary>
    /// Папка «установки» из командной строки (<c>--install-root путь</c>) — для проверок: программа, ярлыки
    /// и запись удаления (файлом, не в реестре) ставятся внутрь неё; null — обычная установка.
    /// </summary>
    public static string? InstallRootOverride { get; private set; }

    internal static string? ParseSettingsPath(IReadOnlyList<string> args) => ParsePathArg(args, "--settings");

    internal static string? ParsePathArg(IReadOnlyList<string> args, string name)
    {
        for (int i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args[i + 1]))
                return System.IO.Path.GetFullPath(args[i + 1]);
        return null;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        SettingsPathOverride = ParseSettingsPath(e.Args);
        InstallRootOverride = ParsePathArg(e.Args, "--install-root");
        if (e.Args.Any(a => string.Equals(a, Install.AppInstaller.UninstallArg, StringComparison.OrdinalIgnoreCase)))
        {
            // Удаление из «Приложений» Windows: главное окно не открываем.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { Install.InstallFlow.RunUninstall(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Удаление ZI-scope", MessageBoxButton.OK, MessageBoxImage.Error); }
            Shutdown();
            return;
        }
        // Тема — до создания окна.
        Theme.Apply(this);
        base.OnStartup(e);
        // Непойманная ошибка в UI не должна молча закрывать программу посреди работы.
        DispatcherUnhandledException += OnUnhandled;
        // Окно создаётся здесь, а не через StartupUri: при «--uninstall» его открывать не нужно.
        new MainWindow().Show();
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "ZI-scope — ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
