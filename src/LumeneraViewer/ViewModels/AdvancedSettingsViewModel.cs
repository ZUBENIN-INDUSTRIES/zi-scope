using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.Drivers;

namespace LumeneraViewer.ViewModels;

/// <summary>Диалог «Дополнительно»: тонкие настройки камеры и служебные действия.</summary>
public partial class AdvancedSettingsViewModel : ObservableObject
{
    private readonly MainViewModel _host;

    [ObservableProperty] private double? _cameraIndex;
    [ObservableProperty] private bool _swapRedBlue;
    /// <summary>0 — Быстрый (1), 1 — Качественный (2), 2 — Лучший (3).</summary>
    [ObservableProperty] private int _demosaicIndex;

    public IReadOnlyList<string> DemosaicMethods { get; } = new[] { "Быстрый", "Качественный", "Лучший" };

    public AdvancedSettingsViewModel(MainViewModel host)
    {
        _host = host;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var c = _host.Settings.Camera;
        CameraIndex = c.CameraIndex;
        SwapRedBlue = c.SwapRedBlue;
        DemosaicIndex = Math.Clamp(c.DemosaicMethod, 1, 3) - 1;
    }

    /// <summary>Записать изменения; если что-то поменялось — переподключить реальную камеру.</summary>
    public async Task ApplyAsync()
    {
        var c = _host.Settings.Camera;
        var index = (int)Math.Clamp(Math.Round(CameraIndex ?? 1), 1, 8);
        var method = DemosaicIndex + 1;
        if (c.CameraIndex == index && c.SwapRedBlue == SwapRedBlue && c.DemosaicMethod == method) return;
        c.CameraIndex = index;
        c.SwapRedBlue = SwapRedBlue;
        c.DemosaicMethod = method;
        _host.SaveSettingsSoon();
        if (_host.State == ConnectionState.Demo)
            _host.Notify("Настройки камеры сохранены", "Применятся при подключении реальной камеры.", NotifyKind.Info);
        else
            await _host.ConnectAsync();
    }

    [RelayCommand] private Task InstallDrivers() => DriverStartupFlow.ReinstallAsync(_host);

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        var dir = Path.GetDirectoryName(_host.SettingsPath)!;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    [RelayCommand]
    private void ResetSettings()
    {
        _host.ResetSettings();
        LoadFromSettings();
    }
}
