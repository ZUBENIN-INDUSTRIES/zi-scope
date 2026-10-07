using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using LumeneraViewer.Camera;

namespace LumeneraViewer.Drivers;

public enum DriverInstallResult { Success, SuccessRebootRequired, Cancelled, InstallerNotFound, Failed, TimedOut }

/// <summary>
/// Наличие драйверов камеры и их установка из комплекта программы (папка Drivers рядом с exe).
/// В сеть не ходит: запускает только локальный установщик.
/// </summary>
public sealed class DriverService
{
    /// <summary>Тихий режим InstallShield: /S — сам setup.exe, /v"…" — аргументы для msiexec.</summary>
    public const string SilentArguments = "/S /v\"/qn REBOOT=ReallySuppress\"";

    public static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);

    private const int ErrorCancelled = 1223;   // пользователь отказал в UAC
    private const int ExitRebootRequired = 3010;

    private readonly Func<bool> _isInstalled;
    private readonly Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<int>> _run;

    public DriverService(string? driversFolder = null)
        : this(driversFolder, CameraFactory.IsDriverAvailable, RunProcessAsync) { }

    /// <summary>Для тестов: подмена проверки драйвера и запуска процесса.</summary>
    internal DriverService(string? driversFolder, Func<bool> isInstalled,
        Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<int>> runInstaller)
    {
        DriversFolder = driversFolder ?? Path.Combine(AppContext.BaseDirectory, "Drivers");
        _isInstalled = isInstalled;
        _run = runInstaller;
    }

    public string DriversFolder { get; }

    /// <summary>Драйвер есть, если lucamapi.dll загружается.</summary>
    public bool IsInstalled() => _isInstalled();

    /// <summary>Первый *.exe в папке драйверов (по алфавиту) или null.</summary>
    public string? FindBundledInstaller()
    {
        if (!Directory.Exists(DriversFolder)) return null;
        return Directory.EnumerateFiles(DriversFolder, "*.exe")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Запуск комплектного установщика с правами администратора. silent=false — обычный мастер.</summary>
    public async Task<DriverInstallResult> InstallAsync(bool silent, CancellationToken ct = default)
    {
        var exe = FindBundledInstaller();
        if (exe is null) return DriverInstallResult.InstallerNotFound;

        var psi = new ProcessStartInfo(exe)
        {
            Arguments = silent ? SilentArguments : "",
            Verb = "runas",               // запрос UAC
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
        };

        int code;
        try
        {
            code = await _run(psi, InstallTimeout, ct);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled) { return DriverInstallResult.Cancelled; }
        catch (OperationCanceledException) { return DriverInstallResult.Cancelled; }
        catch (TimeoutException) { return DriverInstallResult.TimedOut; }
        catch (Exception ex)
        {
            Debug.WriteLine($"Установщик драйверов: {ex}");
            return DriverInstallResult.Failed;
        }

        if (code != 0 && code != ExitRebootRequired) return DriverInstallResult.Failed;
        // Код успеха ещё не значит, что DLL доступна — перепроверяем.
        if (!_isInstalled()) return DriverInstallResult.Failed;
        return code == ExitRebootRequired ? DriverInstallResult.SuccessRebootRequired : DriverInstallResult.Success;
    }

    /// <summary>Запуск процесса и ожидание кода возврата; по таймауту — TimeoutException.</summary>
    private static async Task<int> RunProcessAsync(ProcessStartInfo psi, TimeSpan timeout, CancellationToken ct)
    {
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Установщик не запустился");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
        return p.ExitCode;
    }
}
