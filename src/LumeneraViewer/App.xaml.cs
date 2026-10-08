using System.Windows;
using System.Windows.Threading;
using LumeneraViewer.UI.Effects;

namespace LumeneraViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
