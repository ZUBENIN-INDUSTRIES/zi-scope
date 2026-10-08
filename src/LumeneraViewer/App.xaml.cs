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

    internal static string? ParseSettingsPath(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], "--settings", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args[i + 1]))
                return System.IO.Path.GetFullPath(args[i + 1]);
        return null;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        SettingsPathOverride = ParseSettingsPath(e.Args);
        // Тема — до создания окна (StartupUri открывает его после OnStartup).
        Theme.Apply(this);
        base.OnStartup(e);
        // Непойманная ошибка в UI не должна молча закрывать программу посреди работы.
        DispatcherUnhandledException += OnUnhandled;
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "ZI-scope — ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
