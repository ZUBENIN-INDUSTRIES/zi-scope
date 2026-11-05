using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace LumeneraViewer.Install;

/// <summary>Запись «Удаление программ» Windows для ZI-scope.</summary>
public sealed record UninstallInfo(string DisplayName, string DisplayVersion, string Publisher, string InstallLocation,
    string DisplayIcon, string UninstallString, long EstimatedSizeKb);

/// <summary>Где хранится запись удаления: в программе — реестр текущего пользователя, в тестах — файл.</summary>
public interface IUninstallRegistry
{
    void Write(UninstallInfo info);
    void Delete();
    /// <summary>Установленная версия; null — записи нет.</summary>
    string? InstalledVersion();
}

/// <summary>HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\ZI-scope — без прав администратора.</summary>
public sealed class HkcuUninstallRegistry : IUninstallRegistry
{
    public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ZI-scope";

    public void Write(UninstallInfo i)
    {
        using var k = Registry.CurrentUser.CreateSubKey(KeyPath);
        k.SetValue("DisplayName", i.DisplayName);
        k.SetValue("DisplayVersion", i.DisplayVersion);
        k.SetValue("Publisher", i.Publisher);
        k.SetValue("InstallLocation", i.InstallLocation);
        k.SetValue("DisplayIcon", i.DisplayIcon);
        k.SetValue("UninstallString", i.UninstallString);
        k.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, i.EstimatedSizeKb), RegistryValueKind.DWord);
        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public void Delete() => Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);

    public string? InstalledVersion()
    {
        using var k = Registry.CurrentUser.OpenSubKey(KeyPath);
        return k?.GetValue("DisplayVersion") as string;
    }
}

/// <summary>Запись удаления в JSON-файле — для проверок (<c>--install-root</c>) и тестов, реестр не трогается.</summary>
public sealed class FileUninstallRegistry : IUninstallRegistry
{
    private readonly string _path;
    public FileUninstallRegistry(string path) => _path = path;

    public void Write(UninstallInfo info)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    public string? InstalledVersion()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<UninstallInfo>(File.ReadAllText(_path))?.DisplayVersion : null; }
        catch (JsonException) { return null; }
    }
}

/// <summary>Куда ставится программа: папка, ярлыки, запись удаления.</summary>
public sealed record InstallTargets(string InstallDir, string StartMenuDir, string DesktopDir, IUninstallRegistry Registry)
{
    /// <summary>Обычная установка для текущего пользователя, без прав администратора.</summary>
    public static InstallTargets Default() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ZI-scope"),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        new HkcuUninstallRegistry());

    /// <summary>Всё внутри одной папки, запись удаления — файл (проверки, тесты).</summary>
    public static InstallTargets UnderRoot(string root) => new(
        Path.Combine(root, "Programs", "ZI-scope"), Path.Combine(root, "StartMenu"), Path.Combine(root, "Desktop"),
        new FileUninstallRegistry(Path.Combine(root, "uninstall.json")));

    public string InstalledExe => Path.Combine(InstallDir, AppInstaller.ExeName);
    public string StartMenuShortcut => Path.Combine(StartMenuDir, "ZI-scope.lnk");
    public string DesktopShortcut => Path.Combine(DesktopDir, "ZI-scope.lnk");
}

/// <summary>
/// Установка ZI-scope «как обычной программы» без отдельного установщика: exe копируется в
/// <c>%LocalAppData%\Programs\ZI-scope</c>, ярлыки — в меню «Пуск» и на рабочий стол, запись — в «Приложения»
/// Windows (удаление запускает <c>ZI-scope.exe --uninstall</c>). Права администратора не нужны.
/// </summary>
public static class AppInstaller
{
    public const string ExeName = "ZI-scope.exe";
    public const string UninstallArg = "--uninstall";

    /// <summary>Создатель ярлыков: (путь .lnk, путь exe). В программе — WScript.Shell; в тестах можно подменить.</summary>
    public static Action<string, string> CreateShortcut { get; set; } = CreateShellShortcut;

    public static bool IsInstalled(InstallTargets t) => File.Exists(t.InstalledExe);

    /// <summary>Программа запущена из папки установки.</summary>
    public static bool IsRunningInstalled(InstallTargets t, string? currentExe) =>
        currentExe is not null && string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(t.InstalledExe), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Установить (или обновить) копию <paramref name="sourceExe"/>. Занятый файл (запущена установленная копия) —
    /// <see cref="IOException"/>, прежняя установка не тронута.
    /// </summary>
    public static void Install(InstallTargets t, string sourceExe, string version, string publisher, bool desktopShortcut)
    {
        if (!File.Exists(sourceExe)) throw new FileNotFoundException("Нет файла программы", sourceExe);
        Directory.CreateDirectory(t.InstallDir);
        var tmp = t.InstalledExe + ".new";
        File.Copy(sourceExe, tmp, overwrite: true);
        try { File.Move(tmp, t.InstalledExe, overwrite: true); }
        catch
        {
            TryDelete(tmp);
            throw;
        }

        Directory.CreateDirectory(t.StartMenuDir);
        CreateShortcut(t.StartMenuShortcut, t.InstalledExe);
        if (desktopShortcut)
        {
            Directory.CreateDirectory(t.DesktopDir);
            CreateShortcut(t.DesktopShortcut, t.InstalledExe);
        }
        var size = new FileInfo(t.InstalledExe).Length / 1024;
        t.Registry.Write(new UninstallInfo("ZI-scope", version, publisher, t.InstallDir, t.InstalledExe,
            $"\"{t.InstalledExe}\" {UninstallArg}", size));
    }

    /// <summary>
    /// Убрать ярлыки и запись удаления. Сам exe запущен и удалить себя не может — папку установки удаляет
    /// <see cref="ScheduleFolderRemoval"/> после выхода. Настройки в %AppData% и снимки остаются.
    /// </summary>
    public static void Uninstall(InstallTargets t)
    {
        TryDelete(t.StartMenuShortcut);
        TryDelete(t.DesktopShortcut);
        t.Registry.Delete();
    }

    /// <summary>
    /// Удалить папку установки, когда программа выйдет: короткий .cmd во временной папке ждёт, пока процесс
    /// <paramref name="processId"/> завершится, удаляет папку и сам себя. Рабочая папка — %TEMP%, чтобы не держать удаляемую.
    /// </summary>
    public static void ScheduleFolderRemoval(string folder, int processId)
    {
        // Скрипт только из ASCII, а путь (может быть с кириллицей) приходит аргументом — без проблем с кодировкой cmd.
        var cmdFile = Path.Combine(Path.GetTempPath(), $"zi-scope-uninstall-{processId}.cmd");
        File.WriteAllLines(cmdFile, new[]
        {
            "@echo off",
            ":wait",
            // Полные пути: в PATH может оказаться чужой find (например, из Git)
            "\"%SystemRoot%\\System32\\tasklist.exe\" /fi \"PID eq %~2\" | \"%SystemRoot%\\System32\\find.exe\" \"%~2\" >nul" +
                " && (\"%SystemRoot%\\System32\\ping.exe\" -n 2 127.0.0.1 >nul & goto wait)",
            "rmdir /s /q \"%~1\"",
            "del \"%~f0\"",
        });
        var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetTempPath(),
        };
        psi.Arguments = $"/c \"\"{cmdFile}\" \"{folder}\" {processId}\"";
        System.Diagnostics.Process.Start(psi);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static void CreateShellShortcut(string lnk, string target)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Не удалось создать ярлык: нет WScript.Shell");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic s = shell.CreateShortcut(lnk);
            s.TargetPath = target;
            s.WorkingDirectory = Path.GetDirectoryName(target);
            s.IconLocation = target + ",0";
            s.Description = "ZI-scope — программа для микроскопных камер Lumenera";
            s.Save();
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(s);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}
