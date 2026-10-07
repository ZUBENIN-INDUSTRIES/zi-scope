using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using LumeneraViewer.Drivers;

namespace LumeneraViewer.Tests;

public sealed class DriverServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lv-drv-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string Touch(string name)
    {
        Directory.CreateDirectory(_dir);
        var p = Path.Combine(_dir, name);
        File.WriteAllBytes(p, [0x4D, 0x5A]);
        return p;
    }

    [Fact]
    public void FindBundledInstaller_MissingFolder_ReturnsNull()
    {
        Assert.Null(new DriverService(_dir).FindBundledInstaller());
    }

    [Fact]
    public void FindBundledInstaller_FolderWithoutExe_ReturnsNull()
    {
        Touch("readme.txt");
        Assert.Null(new DriverService(_dir).FindBundledInstaller());
    }

    [Fact]
    public void FindBundledInstaller_ReturnsFirstExeAlphabetically()
    {
        Touch("zeta_setup.exe");
        Touch("notes.txt");
        var expected = Touch("infinity_analyze_and_capture_for_windows_v_6_5_6.exe");
        Assert.Equal(expected, new DriverService(_dir).FindBundledInstaller());
    }

    // ───── InstallAsync: сам установщик не запускается, процесс подменён ─────

    private sealed class FakeRunner
    {
        public ProcessStartInfo? Psi;
        public TimeSpan Timeout;
        public Func<int> Result = () => 0;

        public Task<int> Run(ProcessStartInfo psi, TimeSpan timeout, CancellationToken ct)
        {
            Psi = psi;
            Timeout = timeout;
            return Task.FromResult(Result());
        }
    }

    private DriverService Service(FakeRunner r, bool installedAfter = true)
        => new(_dir, () => installedAfter, r.Run);

    [Fact]
    public async Task Install_NoInstaller_ReturnsInstallerNotFound_WithoutStartingProcess()
    {
        var r = new FakeRunner();
        Assert.Equal(DriverInstallResult.InstallerNotFound, await Service(r).InstallAsync(silent: true));
        Assert.Null(r.Psi);
    }

    [Fact]
    public async Task Install_Silent_RunsBundledExeElevatedWithQuietArgsAndTenMinuteTimeout()
    {
        var exe = Touch("setup.exe");
        var r = new FakeRunner();
        Assert.Equal(DriverInstallResult.Success, await Service(r).InstallAsync(silent: true));
        Assert.Equal(exe, r.Psi!.FileName);
        Assert.Equal("runas", r.Psi.Verb);
        Assert.True(r.Psi.UseShellExecute);
        Assert.Equal("/S /v\"/qn REBOOT=ReallySuppress\"", r.Psi.Arguments);
        Assert.Equal(TimeSpan.FromMinutes(10), r.Timeout);
    }

    [Fact]
    public async Task Install_Wizard_RunsWithoutArguments()
    {
        Touch("setup.exe");
        var r = new FakeRunner();
        await Service(r).InstallAsync(silent: false);
        Assert.Equal("", r.Psi!.Arguments);
        Assert.Equal("runas", r.Psi.Verb);
    }

    [Theory]
    [InlineData(0, true, DriverInstallResult.Success)]
    [InlineData(3010, true, DriverInstallResult.SuccessRebootRequired)]
    [InlineData(1603, true, DriverInstallResult.Failed)]
    [InlineData(0, false, DriverInstallResult.Failed)]      // код 0, но DLL так и не грузится
    [InlineData(3010, false, DriverInstallResult.Failed)]
    public async Task Install_MapsExitCodeAndRechecksDriver(int exitCode, bool dllLoads, DriverInstallResult expected)
    {
        Touch("setup.exe");
        var r = new FakeRunner { Result = () => exitCode };
        Assert.Equal(expected, await Service(r, dllLoads).InstallAsync(silent: true));
    }

    [Fact]
    public async Task Install_UacDeclined_ReturnsCancelled()
    {
        Touch("setup.exe");
        var r = new FakeRunner { Result = () => throw new Win32Exception(1223) };
        Assert.Equal(DriverInstallResult.Cancelled, await Service(r).InstallAsync(silent: true));
    }

    [Fact]
    public async Task Install_OtherWin32Error_ReturnsFailed()
    {
        Touch("setup.exe");
        var r = new FakeRunner { Result = () => throw new Win32Exception(2) };
        Assert.Equal(DriverInstallResult.Failed, await Service(r).InstallAsync(silent: true));
    }

    [Fact]
    public async Task Install_Timeout_ReturnsTimedOut()
    {
        Touch("setup.exe");
        var r = new FakeRunner { Result = () => throw new TimeoutException() };
        Assert.Equal(DriverInstallResult.TimedOut, await Service(r).InstallAsync(silent: true));
    }

    [Fact]
    public void DefaultFolder_IsDriversNextToProgram()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Drivers"), new DriverService().DriversFolder);
    }
}
