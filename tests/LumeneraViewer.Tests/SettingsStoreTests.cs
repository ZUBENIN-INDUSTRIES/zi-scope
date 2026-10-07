using System.IO;
using System.Text.Json;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lv-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "sub", "settings.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(1, s.Camera.CameraIndex);
        Assert.Equal(2, s.Camera.DemosaicMethod);
        Assert.Equal("Объектив 1", s.CurrentObjective);
        Assert.Single(s.Objectives);
        Assert.Equal(ImageFormatKind.Png, s.Export.Format);
        Assert.Equal(95, s.Export.JpegQuality);
        Assert.Equal(1400, s.Window.Width);
        Assert.True(s.Adjustments.IsIdentity);
    }

    [Fact]
    public void BrokenJson_RenamedToBak_AndDefaultsReturned()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ \"Camera\": { oops");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(1, s.Camera.CameraIndex);
        Assert.True(File.Exists(FilePath + ".bak"));
        Assert.Equal("{ \"Camera\": { oops", File.ReadAllText(FilePath + ".bak"));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void RoundTrip_AllFields_EnumsAsStrings()
    {
        var store = new SettingsStore(FilePath);
        var a = new AppSettings
        {
            Camera = new CameraSettings { CameraIndex = 3, SwapRedBlue = true, DemosaicMethod = 1, PreferDemo = true },
            Adjustments = new ImageAdjustments(10, -20, 1.5, 80, 30, -5, true, true),
            ExposureMs = 42.5f,
            Gain = 2.25f,
            Presets = { new AdjustmentPreset("Окраска", new ImageAdjustments(Contrast: 15), 12f, null) },
            Objectives = { new ObjectiveProfile("40x", 0.1625) },
            CurrentObjective = "40x",
            ScaleBar = new ScaleBarSettings
            {
                Enabled = false, Corner = ScaleBarCorner.TopLeft, Color = OverlayColor.Yellow, Background = false,
                FontSize = 22, Thickness = 6, FixedMicrons = 50, Unit = LengthUnit.Millimeters, CustomX = 0.25, CustomY = 0.75,
            },
            Guides = new GuideSettings { Crosshair = true, Grid = true, GridStepMicrons = 25, GridStepPercent = 5 },
            Export = new ExportSettings
            {
                Folder = @"D:\Снимки", Format = ImageFormatKind.Tiff, JpegQuality = 80, TiffLzw = true,
                BurnOverlays = false, AlsoSaveClean = true, WriteMetadata = false, Prefix = "Lab",
            },
            Timelapse = new TimelapseSettings { IntervalSeconds = 2.5, Count = 7 },
            Window = new WindowSettings { Width = 1000, Height = 700, Maximized = true, PanelCollapsed = true },
        };
        a.Extensions["future"] = JsonDocument.Parse("{\"x\":1}").RootElement.Clone();

        store.Save(a);
        var text = File.ReadAllText(FilePath);
        Assert.Contains("\"Tiff\"", text);
        Assert.Contains("\"TopLeft\"", text);

        var b = new SettingsStore(FilePath).Load();
        Assert.Equal(3, b.Camera.CameraIndex);
        Assert.True(b.Camera.SwapRedBlue);
        Assert.Equal(1, b.Camera.DemosaicMethod);
        Assert.True(b.Camera.PreferDemo);
        Assert.Equal(new ImageAdjustments(10, -20, 1.5, 80, 30, -5, true, true), b.Adjustments);
        Assert.Equal(42.5f, b.ExposureMs);
        Assert.Equal(2.25f, b.Gain);
        var p = Assert.Single(b.Presets);
        Assert.Equal("Окраска", p.Name);
        Assert.Equal(15, p.Adjustments.Contrast);
        Assert.Equal(12f, p.ExposureMs);
        Assert.Null(p.Gain);
        Assert.Equal(2, b.Objectives.Count);
        Assert.Equal(new ObjectiveProfile("40x", 0.1625), b.Objectives[1]);
        Assert.Equal("40x", b.CurrentObjective);
        Assert.False(b.ScaleBar.Enabled);
        Assert.Equal(ScaleBarCorner.TopLeft, b.ScaleBar.Corner);
        Assert.Equal(OverlayColor.Yellow, b.ScaleBar.Color);
        Assert.False(b.ScaleBar.Background);
        Assert.Equal(22, b.ScaleBar.FontSize);
        Assert.Equal(6, b.ScaleBar.Thickness);
        Assert.Equal(50, b.ScaleBar.FixedMicrons);
        Assert.Equal(LengthUnit.Millimeters, b.ScaleBar.Unit);
        Assert.Equal(0.25, b.ScaleBar.CustomX);
        Assert.Equal(0.75, b.ScaleBar.CustomY);
        Assert.True(b.Guides.Crosshair);
        Assert.True(b.Guides.Grid);
        Assert.Equal(25, b.Guides.GridStepMicrons);
        Assert.Equal(5, b.Guides.GridStepPercent);
        Assert.Equal(@"D:\Снимки", b.Export.Folder);
        Assert.Equal(ImageFormatKind.Tiff, b.Export.Format);
        Assert.Equal(80, b.Export.JpegQuality);
        Assert.True(b.Export.TiffLzw);
        Assert.False(b.Export.BurnOverlays);
        Assert.True(b.Export.AlsoSaveClean);
        Assert.False(b.Export.WriteMetadata);
        Assert.Equal("Lab", b.Export.Prefix);
        Assert.Equal(2.5, b.Timelapse.IntervalSeconds);
        Assert.Equal(7, b.Timelapse.Count);
        Assert.Equal(1000, b.Window.Width);
        Assert.Equal(700, b.Window.Height);
        Assert.True(b.Window.Maximized);
        Assert.True(b.Window.PanelCollapsed);
        Assert.Equal(1, b.Extensions["future"].GetProperty("x").GetInt32());
    }

    // Опрос файла вместо фиксированной паузы
    private static bool WaitFor(Func<bool> cond, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try { if (cond()) return true; } catch (IOException) { }
            Thread.Sleep(50);
        }
        return false;
    }

    [Fact]
    public void ScheduleSave_WritesAfterDelay_FlushWritesImmediately()
    {
        using var store = new SettingsStore(FilePath);
        store.ScheduleSave(new AppSettings { CurrentObjective = "A" });
        Assert.False(File.Exists(FilePath));
        Assert.True(WaitFor(() => File.Exists(FilePath) && new SettingsStore(FilePath).Load().CurrentObjective == "A"));

        store.ScheduleSave(new AppSettings { CurrentObjective = "B" });
        store.Flush();
        Assert.Equal("B", new SettingsStore(FilePath).Load().CurrentObjective);
    }

    [Fact]
    public void ScheduleSave_TakesSnapshotAtCallTime()
    {
        using var store = new SettingsStore(FilePath);
        var s = new AppSettings { CurrentObjective = "Снимок" };
        store.ScheduleSave(s);
        s.CurrentObjective = "Изменено позже";
        store.Flush();
        Assert.Equal("Снимок", new SettingsStore(FilePath).Load().CurrentObjective);
    }

    [Fact]
    public void ScheduleSave_ToUnwritablePath_DoesNotThrowOrCrash()
    {
        // Вместо папки лежит файл — создать settings.json внутри нельзя
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var store = new SettingsStore(Path.Combine(blocker, "settings.json"));
        Exception? failed = null;
        store.SaveFailed += ex => failed = ex;

        store.ScheduleSave(new AppSettings());
        Assert.True(WaitFor(() => failed is not null), "ошибка отложенного сохранения не сообщена");
        store.ScheduleSave(new AppSettings());
        store.Dispose();   // Dispose = Flush, тоже не бросает
    }

    [Fact]
    public void NullFields_LoadedAsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """
            { "Camera": null, "Adjustments": null, "Presets": null, "Objectives": null, "CurrentObjective": null,
              "ScaleBar": null, "Guides": null, "Export": null, "Timelapse": null, "Window": null, "Extensions": null }
            """);
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(1, s.Camera.CameraIndex);
        Assert.True(s.Adjustments.IsIdentity);
        Assert.Empty(s.Presets);
        Assert.Equal("Объектив 1", Assert.Single(s.Objectives).Name);
        Assert.Equal("Объектив 1", s.CurrentObjective);
        Assert.True(s.ScaleBar.Enabled);
        Assert.Equal(100, s.Guides.GridStepMicrons);
        Assert.Equal(95, s.Export.JpegQuality);
        Assert.Equal(10, s.Timelapse.IntervalSeconds);
        Assert.Equal(1400, s.Window.Width);
        Assert.Empty(s.Extensions);
    }
}
