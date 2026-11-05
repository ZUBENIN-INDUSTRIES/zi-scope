using System.IO;
using LumeneraViewer.Install;

namespace LumeneraViewer.Tests;

/// <summary>Установка «как программа»: всё внутри временной папки, запись удаления — файлом, реестр не трогается.</summary>
public sealed class AppInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "zs-install-" + Guid.NewGuid().ToString("N"));
    private readonly List<(string Lnk, string Target)> _shortcuts = new();
    private readonly Action<string, string> _savedShortcut;

    public AppInstallerTests()
    {
        _savedShortcut = AppInstaller.CreateShortcut;
        AppInstaller.CreateShortcut = (lnk, target) =>
        {
            _shortcuts.Add((lnk, target));
            File.WriteAllText(lnk, target);
        };
    }

    public void Dispose()
    {
        AppInstaller.CreateShortcut = _savedShortcut;
        try { Directory.Delete(_root, true); } catch { }
    }

    private string Source(string content = "exe-v1")
    {
        var dir = Path.Combine(_root, "Downloads");
        Directory.CreateDirectory(dir);
        var p = Path.Combine(dir, "ZI-scope.exe");
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void Install_CopiesExe_Shortcuts_UninstallRecord()
    {
        var t = InstallTargets.UnderRoot(_root);
        Assert.False(AppInstaller.IsInstalled(t));

        AppInstaller.Install(t, Source(), "0.1.0-alpha", "ZI", desktopShortcut: true);

        Assert.True(AppInstaller.IsInstalled(t));
        Assert.Equal("exe-v1", File.ReadAllText(t.InstalledExe));
        Assert.False(File.Exists(t.InstalledExe + ".new"));
        Assert.Contains((t.StartMenuShortcut, t.InstalledExe), _shortcuts);
        Assert.Contains((t.DesktopShortcut, t.InstalledExe), _shortcuts);
        Assert.Equal("0.1.0-alpha", t.Registry.InstalledVersion());
        Assert.True(AppInstaller.IsRunningInstalled(t, t.InstalledExe));
        Assert.False(AppInstaller.IsRunningInstalled(t, Source()));
    }

    [Fact]
    public void Install_Again_ReplacesExeAndVersion()
    {
        var t = InstallTargets.UnderRoot(_root);
        AppInstaller.Install(t, Source("exe-v1"), "0.1.0", "ZI", desktopShortcut: false);
        AppInstaller.Install(t, Source("exe-v2"), "0.2.0", "ZI", desktopShortcut: false);

        Assert.Equal("exe-v2", File.ReadAllText(t.InstalledExe));
        Assert.Equal("0.2.0", t.Registry.InstalledVersion());
        Assert.False(File.Exists(t.DesktopShortcut));
    }

    [Fact]
    public void Install_LockedExe_IOException_OldCopyKept()
    {
        var t = InstallTargets.UnderRoot(_root);
        AppInstaller.Install(t, Source("exe-v1"), "0.1.0", "ZI", desktopShortcut: false);
        using (new FileStream(t.InstalledExe, FileMode.Open, FileAccess.Read, FileShare.None))   // «запущена»
        {
            // Занятый файл: Windows отвечает «нет доступа» или ошибкой ввода-вывода — InstallFlow обрабатывает оба
            var ex = Record.Exception(() => AppInstaller.Install(t, Source("exe-v2"), "0.2.0", "ZI", desktopShortcut: false));
            Assert.True(ex is IOException or UnauthorizedAccessException, ex?.GetType().Name);
        }

        Assert.Equal("exe-v1", File.ReadAllText(t.InstalledExe));
        Assert.False(File.Exists(t.InstalledExe + ".new"));
        Assert.Equal("0.1.0", t.Registry.InstalledVersion());
    }

    [Fact]
    public void Uninstall_RemovesShortcutsAndRecord_KeepsNothingElseBroken()
    {
        var t = InstallTargets.UnderRoot(_root);
        AppInstaller.Install(t, Source(), "0.1.0", "ZI", desktopShortcut: true);

        AppInstaller.Uninstall(t);

        Assert.False(File.Exists(t.StartMenuShortcut));
        Assert.False(File.Exists(t.DesktopShortcut));
        Assert.Null(t.Registry.InstalledVersion());
        AppInstaller.Uninstall(t);   // повторно — без ошибок
    }

    [Fact]
    public void UninstallString_RunsInstalledExeWithFlag()
    {
        var t = InstallTargets.UnderRoot(_root);
        AppInstaller.Install(t, Source(), "0.1.0", "ZI", desktopShortcut: false);
        var json = File.ReadAllText(Path.Combine(_root, "uninstall.json"));
        Assert.Contains("--uninstall", json);
        Assert.Contains("ZI-scope.exe", json);
    }

    [Fact]
    public void InstallRootArg_Parsed()
    {
        Assert.Equal(Path.GetFullPath(@"C:\x\root"), App.ParsePathArg(new[] { "--install-root", @"C:\x\root" }, "--install-root"));
        Assert.Null(App.ParsePathArg(new[] { "--settings", "a.json" }, "--install-root"));
    }

    [Fact]
    public async Task ScheduleFolderRemoval_WaitsForProcessExit_ThenRemovesFolder_CyrillicPath()
    {
        var folder = Path.Combine(_root, "Программы", "ZI-scope");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ZI-scope.exe"), "x");
        // «Программа» — процесс, который живёт ~3 с
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ping", "-n 4 127.0.0.1")
            { CreateNoWindow = true, UseShellExecute = false })!;

        AppInstaller.ScheduleFolderRemoval(folder, p.Id);

        await Task.Delay(1000);
        Assert.True(Directory.Exists(folder));                     // пока процесс жив — папка на месте
        await p.WaitForExitAsync();
        for (var i = 0; i < 40 && Directory.Exists(folder); i++) await Task.Delay(250);
        Assert.False(Directory.Exists(folder));
    }
}
