using System.IO;
using LumeneraViewer.Export;
using LumeneraViewer.Imaging;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class ProfileFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "zs-prof-" + Guid.NewGuid().ToString("N"));

    public ProfileFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string P(string name) => Path.Combine(_dir, name);

    [Fact]
    public void Color_RoundTrip_WithFormatMarker()
    {
        var p = new ColorProfile
        {
            Name = "Гематоксилин", Adjustments = new ImageAdjustments(5, 10, 0.9, 120, -15, 3),
            IncludesExposure = true, ExposureMs = 17.5f, Gain = 2f,
        };
        ProfileFiles.WriteColor(P("a.zcolor"), p);
        Assert.Contains("\"format\": \"zi-scope-color/1\"", File.ReadAllText(P("a.zcolor")));

        var r = ProfileFiles.ReadColor(P("a.zcolor"));
        Assert.Equal("Гематоксилин", r.Name);
        Assert.Equal(p.Adjustments, r.Adjustments);
        Assert.True(r.IncludesExposure);
        Assert.Equal(17.5f, r.ExposureMs);
        Assert.Equal(2f, r.Gain);
    }

    [Fact]
    public void Config_RoundTrip_AllSettings()
    {
        var s = new AppSettings { CurrentMagnification = 200, LastSeriesFolder = @"D:\Серия", LegacyCalibrationDismissed = true };
        s.Export.Format = ImageFormatKind.Tiff;
        s.Guides.Grid = true;
        MagnificationSettings.GetOrCreate(s, 200).MicronsPerPixel = 0.41;
        MagnificationSettings.GetOrCreate(s, 200).Adjustments = new ImageAdjustments(Contrast: 30);
        s.ColorProfiles.Add(new ColorProfile { Name = "Синий" });
        s.SeriesDefaults.Groups = 3;
        s.SeriesDefaults.FileNameTemplate = "{series}_{group}";

        ProfileFiles.WriteConfig(P("c.zscope"), s);
        var text = File.ReadAllText(P("c.zscope"));
        Assert.Contains("\"format\": \"zi-scope-config/1\"", text);

        var r = ProfileFiles.ReadConfig(P("c.zscope"));
        Assert.Equal(200, r.CurrentMagnification);
        Assert.Equal(@"D:\Серия", r.LastSeriesFolder);
        Assert.True(r.LegacyCalibrationDismissed);
        Assert.Equal(ImageFormatKind.Tiff, r.Export.Format);
        Assert.True(r.Guides.Grid);
        var m200 = r.Magnifications.Single(m => m.Magnification == 200);
        Assert.Equal(0.41, m200.MicronsPerPixel);
        Assert.Equal(30, m200.Adjustments.Contrast);
        Assert.Equal("Синий", Assert.Single(r.ColorProfiles).Name);
        Assert.Equal(3, r.SeriesDefaults.Groups);
        Assert.Equal("{series}_{group}", r.SeriesDefaults.FileNameTemplate);
        Assert.Equal(s.SeriesDefaults.Kinds, r.SeriesDefaults.Kinds);
    }

    [Theory]
    [InlineData("{ oops")]
    [InlineData("")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"Name\": \"без отметки формата\" }")]
    [InlineData("{ \"format\": \"zi-scope-config/1\", \"Name\": \"конфигурация вместо цвета\" }")]
    public void Color_BrokenOrForeign_InvalidDataWithRussianText(string content)
    {
        File.WriteAllText(P("x.zcolor"), content);
        var ex = Assert.Throws<InvalidDataException>(() => ProfileFiles.ReadColor(P("x.zcolor")));
        Assert.Equal("Файл не является профилем ZI-scope", ex.Message);
    }

    [Theory]
    [InlineData("{ \"format\": \"zi-scope-config/1\", \"Camera\": { oops")]
    [InlineData("{ \"format\": \"zi-scope-color/1\" }")]
    [InlineData("\u0000\u0001binary")]
    public void Config_BrokenOrForeign_InvalidData(string content)
    {
        File.WriteAllText(P("x.zscope"), content);
        var ex = Assert.Throws<InvalidDataException>(() => ProfileFiles.ReadConfig(P("x.zscope")));
        Assert.Equal("Файл не является профилем ZI-scope", ex.Message);
    }

    [Fact]
    public void FutureVersion_UnknownFieldsIgnored_KnownLoaded()
    {
        File.WriteAllText(P("f.zcolor"), """
            { "format": "zi-scope-color/2", "Name": "Будущий", "Sparkle": 42,
              "Adjustments": { "Brightness": 7, "Hologram": { "x": 1 } }, "IncludesExposure": false }
            """);
        var c = ProfileFiles.ReadColor(P("f.zcolor"));
        Assert.Equal("Будущий", c.Name);
        Assert.Equal(7, c.Adjustments.Brightness);

        File.WriteAllText(P("f.zscope"), """
            { "format": "zi-scope-config/3", "CurrentMagnification": 500, "NewThing": [1, 2, 3],
              "Guides": { "Grid": true, "Spiral": true } }
            """);
        var s = ProfileFiles.ReadConfig(P("f.zscope"));
        Assert.Equal(500, s.CurrentMagnification);
        Assert.True(s.Guides.Grid);
        Assert.Contains(s.Magnifications, m => m.Magnification == 500);   // профиль текущего создан
        Assert.Equal(95, s.Export.JpegQuality);                           // остальное — умолчания
    }
}
