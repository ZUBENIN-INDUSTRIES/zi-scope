using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using LumeneraViewer.Camera;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class FrameExporterTests : IDisposable
{
    private static readonly DateTime T = new(2026, 10, 7, 14, 5, 9, 42);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lv-export-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void BuildFileName_FullTemplate()
    {
        Assert.Equal("Lumenera_20261007_140509_042_10x_0007.png",
            FrameExporter.BuildFileName("Lumenera", T, "10x", 7, ImageFormatKind.Png));
    }

    [Fact]
    public void BuildFileName_WithoutObjectiveAndSeries()
    {
        Assert.Equal("Sample_20261007_140509_042.jpg",
            FrameExporter.BuildFileName("Sample", T, null, null, ImageFormatKind.Jpeg));
        Assert.Equal("Sample_20261007_140509_042.tif",
            FrameExporter.BuildFileName("Sample", T, "  ", null, ImageFormatKind.Tiff));
    }

    [Fact]
    public void BuildFileName_ReplacesInvalidCharacters()
    {
        Assert.Equal("a_b_20261007_140509_042_Объектив 1_40x_.bmp",
            FrameExporter.BuildFileName("a/b", T, "Объектив 1:40x?", null, ImageFormatKind.Bmp));
    }

    [Fact]
    public void BuildFileName_EmptyPrefixFallsBackToDefault()
    {
        Assert.StartsWith("Lumenera_20261007", FrameExporter.BuildFileName("", T, null, null, ImageFormatKind.Png));
    }

    private static CameraFrame Frame(int w = 64, int h = 48)
    {
        var px = new byte[w * h * 3];
        for (var i = 0; i < px.Length; i++) px[i] = (byte)(i % 251);
        return new CameraFrame { Bgr24 = px, Width = w, Height = h };
    }

    private static CaptureMetadata Meta() => new(T, "Simulated", "SIM-1", true, 12.5f, 2f,
        new ImageAdjustments(Brightness: 10), "10x", 0.65);

    private ExportSettings Options(ImageFormatKind f) => new() { Folder = _dir, Format = f, Prefix = "t" };

    private static BitmapSource Decode(string path)
    {
        using var fs = File.OpenRead(path);
        return BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
    }

    [Theory]
    [InlineData(ImageFormatKind.Png)]
    [InlineData(ImageFormatKind.Jpeg)]
    [InlineData(ImageFormatKind.Tiff)]
    [InlineData(ImageFormatKind.Bmp)]
    [InlineData(ImageFormatKind.JpegXr)]
    public async Task SaveAsync_EachFormat_WritesDecodableFileOfFullSize(ImageFormatKind f)
    {
        var path = await new FrameExporter().SaveAsync(Frame(), null, Meta(), Options(f));

        Assert.True(File.Exists(path));
        Assert.Equal(FrameExporter.Extension(f), Path.GetExtension(path));
        var img = Decode(path);
        Assert.Equal(64, img.PixelWidth);
        Assert.Equal(48, img.PixelHeight);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task SaveAsync_TiffLzw_IsSmallerThanUncompressed()
    {
        var flat = new CameraFrame { Bgr24 = new byte[200 * 100 * 3], Width = 200, Height = 100 };
        var plain = await new FrameExporter().SaveAsync(flat, null, Meta(), Options(ImageFormatKind.Tiff));
        var o = Options(ImageFormatKind.Tiff); o.TiffLzw = true; o.Prefix = "lzw";
        var lzw = await new FrameExporter().SaveAsync(flat, null, Meta(), o);
        Assert.True(new FileInfo(lzw).Length < new FileInfo(plain).Length / 4);
    }

    [Fact]
    public async Task SaveAsync_WritesMetadataJson()
    {
        var path = await new FrameExporter().SaveAsync(Frame(), null, Meta(), Options(ImageFormatKind.Png), seriesIndex: 3);

        Assert.EndsWith("_0003.png", path);
        var json = Path.ChangeExtension(path, ".json");
        Assert.True(File.Exists(json));
        using var doc = JsonDocument.Parse(File.ReadAllText(json));
        var r = doc.RootElement;
        Assert.Equal(64, r.GetProperty("Width").GetInt32());
        Assert.Equal(48, r.GetProperty("Height").GetInt32());
        Assert.Equal("Png", r.GetProperty("Format").GetString());
        Assert.Equal(12.5, r.GetProperty("ExposureMs").GetDouble());
        Assert.Equal(2, r.GetProperty("Gain").GetDouble());
        Assert.Equal("10x", r.GetProperty("Objective").GetString());
        Assert.Equal(0.65, r.GetProperty("MicronsPerPixel").GetDouble());
        Assert.Equal("SIM-1", r.GetProperty("CameraSerial").GetString());
        Assert.Equal(10, r.GetProperty("Adjustments").GetProperty("Brightness").GetDouble());
        Assert.Equal(2026, r.GetProperty("Time").GetDateTime().Year);
    }

    [Fact]
    public async Task SaveAsync_NoMetadataWhenDisabled()
    {
        var o = Options(ImageFormatKind.Png); o.WriteMetadata = false;
        var path = await new FrameExporter().SaveAsync(Frame(), null, Meta(), o);
        Assert.False(File.Exists(Path.ChangeExtension(path, ".json")));
    }

    [Fact]
    public async Task SaveAsync_BurnsOverlaysAndKeepsCleanCopy()
    {
        var black = new CameraFrame { Bgr24 = new byte[400 * 300 * 3], Width = 400, Height = 300 };
        var overlays = new OverlayState { MicronsPerPixel = 1, ScaleBarEnabled = true, Color = OverlayColor.White, Background = false };
        var o = Options(ImageFormatKind.Png); o.AlsoSaveClean = true;

        var path = await new FrameExporter().SaveAsync(black, overlays, Meta(), o);

        var clean = Path.Combine(_dir, Path.GetFileNameWithoutExtension(path) + "_clean.png");
        Assert.True(File.Exists(clean));
        Assert.True(MaxByte(Decode(path)) > 200);   // плашка белая на чёрном
        Assert.Equal(0, MaxByte(Decode(clean)));
    }

    [Fact]
    public async Task SaveAsync_SameTimestampTwice_DoesNotOverwrite()
    {
        var ex = new FrameExporter();
        var a = ex.SaveAsync(Frame(), null, Meta(), Options(ImageFormatKind.Png));
        var b = ex.SaveAsync(Frame(), null, Meta(), Options(ImageFormatKind.Png));
        var paths = await Task.WhenAll(a, b);
        Assert.NotEqual(paths[0], paths[1]);
        Assert.Equal(2, Directory.GetFiles(_dir, "*.png").Length);
    }

    [Fact]
    public async Task SaveAsync_UnwritableFolder_Throws()
    {
        var file = Path.Combine(_dir, "not-a-folder");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(file, "x");
        var o = Options(ImageFormatKind.Png); o.Folder = file;
        await Assert.ThrowsAnyAsync<IOException>(() => new FrameExporter().SaveAsync(Frame(), null, Meta(), o));
    }

    private static int MaxByte(BitmapSource img)
    {
        var bgr = new FormatConvertedBitmap(img, System.Windows.Media.PixelFormats.Bgr24, null, 0);
        var px = new byte[bgr.PixelWidth * bgr.PixelHeight * 3];
        bgr.CopyPixels(px, bgr.PixelWidth * 3, 0);
        return px.Max();
    }

    [Theory]
    [InlineData(ImageFormatKind.Png, ".png")]
    [InlineData(ImageFormatKind.Jpeg, ".jpg")]
    [InlineData(ImageFormatKind.Tiff, ".tif")]
    [InlineData(ImageFormatKind.Bmp, ".bmp")]
    [InlineData(ImageFormatKind.JpegXr, ".jxr")]
    public void Extension_PerFormat(ImageFormatKind f, string ext) => Assert.Equal(ext, FrameExporter.Extension(f));
}
