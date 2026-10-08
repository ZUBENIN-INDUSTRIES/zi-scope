using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumeneraViewer.Camera;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

/// <summary>G01: сетка и перекрестие видны только на экране и никогда не впечатываются в файл.</summary>
public sealed class OverlayExportTests : IDisposable
{
    private const int W = 1600, H = 400;   // ExportScale = 1: плашка в полный размер
    // Рабочая папка — рядом со сборкой тестов, не во временной папке пользователя.
    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "overlay-export-" + Guid.NewGuid().ToString("N"));
    private readonly FrameExporter _exp = new();

    public void Dispose()
    {
        _exp.Dispose();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static CameraFrame Black() => new() { Bgr24 = new byte[W * H * 3], Width = W, Height = H };

    private static CaptureMetadata Meta() => new(DateTime.Now, "Simulated", "SIM-1", true, 10f, 1f,
        new ImageAdjustments(), "×100", 1.0);

    private ExportSettings Options() => new()
    {
        Folder = _dir, Format = ImageFormatKind.Png, Prefix = "g", BurnOverlays = true, AlsoSaveClean = true, WriteMetadata = false,
    };

    private static readonly OverlayState GuidesOnly = new() { Grid = true, Crosshair = true, GridStepPercent = 10, Color = OverlayColor.White };

    private static byte[] Pixels(string path)
    {
        using var fs = File.OpenRead(path);
        var src = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var bgr = new FormatConvertedBitmap(src, PixelFormats.Bgr24, null, 0);
        var px = new byte[W * H * 3];
        bgr.CopyPixels(px, W * 3, 0);
        return px;
    }

    private static int At(byte[] px, int x, int y) => Math.Max(px[(y * W + x) * 3], Math.Max(px[(y * W + x) * 3 + 1], px[(y * W + x) * 3 + 2]));

    [Fact]
    public async Task SaveAsync_GridAndCrosshairOnly_FileHasNoOverlayPixels()
    {
        var path = await _exp.SaveAsync(Black(), GuidesOnly, Meta(), Options());

        Assert.Equal(0, Pixels(path).Max());
        Assert.Empty(Directory.GetFiles(_dir, "*_clean*"));   // впечатывать нечего — и копии «_clean» не нужно
    }

    [Fact]
    public async Task SaveExactAsync_GridAndCrosshairOnly_FileHasNoOverlayPixels()
    {
        var path = await _exp.SaveExactAsync(Black(), GuidesOnly, Meta(), Options(), Path.Combine(_dir, "exact.png"), overwrite: false);

        Assert.Equal(0, Pixels(path).Max());
    }

    [Fact]
    public async Task SaveAsync_ScaleBarWithGuides_ScaleBarBurned_GuidesNot()
    {
        var s = GuidesOnly with { ScaleBarEnabled = true, MicronsPerPixel = 1.0, Corner = ScaleBarCorner.BottomRight };

        var px = Pixels(await _exp.SaveAsync(Black(), s, Meta(), Options()));

        Assert.True(px.Max() > 200);              // плашка впечатана
        Assert.Equal(0, At(px, W / 2, H / 2 - 20)); // вертикаль перекрестия
        Assert.Equal(0, At(px, 160, 10));           // линия сетки x = 10 % ширины
        Assert.Equal(0, At(px, 40, 5));
    }
}
