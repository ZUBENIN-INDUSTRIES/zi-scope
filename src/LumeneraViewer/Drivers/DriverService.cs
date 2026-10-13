using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using LumeneraViewer.Camera;

namespace LumeneraViewer.Drivers;

public enum DriverInstallResult { Success, SuccessRebootRequired, Cancelled, InstallerNotFound, Failed, TimedOut, TimedOutStillRunning }

/// <summary>Установщик не уложился в таймаут, и завершить его не удалось (например, нет доступа к повышенному процессу).</summary>
public sealed class InstallerStillRunningException : TimeoutException
{
    public InstallerStillRunningException(Exception? inner = null)
        : base("Установщик не ответил вовремя и всё ещё работает", inner) { }
}

/// <summary>
/// Наличие драйверов камеры и их установка из комплекта программы (папка Drivers рядом с exe или установщик, вшитый в exe).
/// В сеть не ходит: запускает только локальный установщик.
/// </summary>
public sealed class DriverService
{
    /// <summary>Тихий режим InstallShield: /S — сам setup.exe, /v"…" — аргументы для msiexec.</summary>
    public const string SilentArguments = "/S /v\"/qn REBOOT=ReallySuppress\"";

    public static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);

    private const int ErrorCancelled = 1223;   // пользователь отказал в UAC
    private const int ExitRebootRequired = 3010;

    /// <summary>Префикс имени ресурса установщика, вшитого в exe (сборка с <c>-p:EmbedDrivers=true</c>).</summary>
    public const string EmbeddedPrefix = "ZI-scope.Drivers.";

    private readonly Func<bool> _isInstalled;
    private readonly Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<int>> _run;
    private readonly EmbeddedInstaller? _embedded;

    public DriverService(string? driversFolder = null)
        : this(driversFolder, CameraFactory.IsDriverAvailable, RunProcessAsync, FromAssembly()) { }

    /// <summary>Для тестов: подмена проверки драйвера, запуска процесса и вшитого установщика.</summary>
    internal DriverService(string? driversFolder, Func<bool> isInstalled,
        Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<int>> runInstaller,
        EmbeddedInstaller? embedded = null, string? extractFolder = null)
    {
        DriversFolder = driversFolder ?? Path.Combine(AppContext.BaseDirectory, "Drivers");
        _isInstalled = isInstalled;
        _run = runInstaller;
        _embedded = embedded;
        ExtractFolder = extractFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZI-scope", "Drivers");
    }

    /// <summary>Установщик, вшитый в exe: имя файла, размер и чтение содержимого.</summary>
    internal sealed record EmbeddedInstaller(string FileName, long Length, Func<Stream> Open);

    public string DriversFolder { get; }

    /// <summary>Куда распаковывается вшитый установщик перед запуском (<c>%LocalAppData%\ZI-scope\Drivers</c>).</summary>
    public string ExtractFolder { get; }

    /// <summary>Драйвер есть, если lucamapi.dll загружается.</summary>
    public bool IsInstalled() => _isInstalled();

    /// <summary>
    /// Первый *.exe в папке драйверов (по алфавиту); если папки нет — путь, куда будет распакован установщик,
    /// вшитый в exe (сам файл появится в <see cref="InstallAsync"/>); иначе null.
    /// </summary>
    public string? FindBundledInstaller()
    {
        if (Directory.Exists(DriversFolder))
        {
            var exe = Directory.EnumerateFiles(DriversFolder, "*.exe")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (exe is not null) return exe;
        }
        return _embedded is null ? null : Path.Combine(ExtractFolder, _embedded.FileName);
    }

    /// <summary>Запуск комплектного установщика с правами администратора. silent=false — обычный мастер.</summary>
    public async Task<DriverInstallResult> InstallAsync(bool silent, CancellationToken ct = default)
    {
        var exe = FindBundledInstaller();
        if (exe is null) return DriverInstallResult.InstallerNotFound;
        if (_embedded is not null && exe == Path.Combine(ExtractFolder, _embedded.FileName))
        {
            try
            {
                await Task.Run(() => Extract(_embedded, exe), ct);
            }
            catch (OperationCanceledException) { return DriverInstallResult.Cancelled; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Не удалось распаковать установщик драйверов: {ex}");
                return DriverInstallResult.Failed;
            }
        }

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
        catch (InstallerStillRunningException) { return DriverInstallResult.TimedOutStillRunning; }
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

    /// <summary>Вшитый в exe установщик или null (обычная сборка).</summary>
    private static EmbeddedInstaller? FromAssembly()
    {
        var asm = typeof(DriverService).Assembly;
        var name = asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(EmbeddedPrefix, StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (name is null) return null;
        using var s = asm.GetManifestResourceStream(name);
        if (s is null) return null;
        return new EmbeddedInstaller(name[EmbeddedPrefix.Length..], s.Length,
            () => asm.GetManifestResourceStream(name) ?? throw new IOException("Ресурс установщика не найден: " + name));
    }

    /// <summary>
    /// Распаковать установщик в <paramref name="path"/>: файл того же размера уже есть — не трогаем,
    /// иначе пишем во временный файл и переименовываем.
    /// </summary>
    internal static void Extract(EmbeddedInstaller e, string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length == e.Length) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var src = e.Open())
        using (var dst = File.Create(tmp))
            src.CopyTo(dst);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Запуск процесса и ожидание кода возврата. По таймауту процесс завершается (Kill) → TimeoutException;
    /// если завершить нельзя (отказ доступа к повышенному процессу) → InstallerStillRunningException.
    /// </summary>
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
            try
            {
                p.Kill(entireProcessTree: true);
                if (!p.WaitForExit(5000)) throw new InstallerStillRunningException();
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException or AggregateException)
            {
                if (!HasExited(p)) throw new InstallerStillRunningException(ex);
            }
            throw new TimeoutException();
        }
        return p.ExitCode;
    }

    private static bool HasExited(Process p)
    {
        try { return p.HasExited; }
        catch (Exception) { return false; }   // нет доступа — считаем, что жив
    }
}
