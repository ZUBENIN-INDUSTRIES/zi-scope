using LumeneraViewer.Camera;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.Drivers;

/// <summary>Заглушка (таск 03 заменит): сценарий драйверов при старте и переустановка.</summary>
public static class DriverStartupFlow
{
    /// <summary>true — драйверы есть, можно подключать реальную камеру.</summary>
    public static Task<bool> EnsureDriversAsync(MainViewModel host) => Task.FromResult(CameraFactory.IsDriverAvailable());

    public static Task ReinstallAsync(MainViewModel host)
    {
        host.Notify("Драйверы", "Установка драйверов ещё не подключена", NotifyKind.Info);
        return Task.CompletedTask;
    }
}
