using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;
using Xunit;

namespace LumeneraViewer.Tests;

public class ScaleBarStyleTests
{
    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("22c55e", 0x22, 0xC5, 0x5E)]
    public void Parse_Hex(string hex, byte r, byte g, byte b) => Assert.Equal(Color.FromRgb(r, g, b), OverlayColors.Parse(hex));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("красный")]
    public void Parse_Garbage_IsNull(string? hex) => Assert.Null(OverlayColors.Parse(hex));

    [Fact]
    public void Colors_DefaultFollowBaseColor_BackgroundContrasts()
    {
        var s = new OverlayState { Color = OverlayColor.Yellow, BackgroundOpacity = 0.55 };
        var (text, bar, bg) = OverlayRenderer.ScaleBarColors(s);
        Assert.Equal(Color.FromRgb(0xFA, 0xCC, 0x15), text);
        Assert.Equal(text, bar);
        Assert.Equal(Colors.Black.R, bg.R);
        Assert.Equal((byte)140, bg.A);   // 0.55 × 255 ≈ 140
    }

    [Fact]
    public void Colors_ExplicitPartsWin()
    {
        var s = new OverlayState { Color = OverlayColor.White, TextColor = Colors.Red, BarColor = Colors.Blue, BackgroundColor = Colors.White, BackgroundOpacity = 1 };
        var (text, bar, bg) = OverlayRenderer.ScaleBarColors(s);
        Assert.Equal(Colors.Red, text);
        Assert.Equal(Colors.Blue, bar);
        Assert.Equal(Color.FromArgb(255, 255, 255, 255), bg);
    }

    [Fact]
    public void FromSettings_ReadsPartColorsAndStyle()
    {
        var a = new AppSettings();
        a.ScaleBar.TextColor = "#EF4444";
        a.ScaleBar.BackgroundOpacity = 2;   // мусор зажимается в 0..1
        a.ScaleBar.Bold = false;
        var s = OverlayState.FromSettings(a);
        Assert.Equal(Color.FromRgb(0xEF, 0x44, 0x44), s.TextColor);
        Assert.Null(s.BarColor);
        Assert.Equal(1, s.BackgroundOpacity);
        Assert.False(s.Bold);
    }

    [Fact]
    public void Preview_UsesSameScaleAsFile()
    {
        // 4K-кадр: в превью плашка того же размера, что впечатается в файл, при любом зуме.
        var frame = new Size(4008, 2672);
        Assert.Equal(OverlayRenderer.ExportScale(4008), OverlayRenderer.PreviewScale(frame));
        var s = new OverlayState { ScaleBarEnabled = true, MicronsPerPixel = 0.5 };
        Assert.Equal(OverlayRenderer.ScaleBarBounds(s, frame, OverlayRenderer.ExportScale(4008)),
                     OverlayRenderer.ScaleBarBounds(s, frame, OverlayRenderer.PreviewScale(frame)));
        // 4008 / 1600 = 2,505 — шрифт 16 станет ≈ 40 px кадра
        Assert.Equal(2.505, OverlayRenderer.PreviewScale(frame), 3);
    }

    [Fact]
    public void BarPixels_HaveChosenColor()
    {
        // Чёрный кадр, линейка красная, без подложки: в центре нижней полосы плашки — красный.
        var frame = new Size(800, 400);
        var s = new OverlayState { ScaleBarEnabled = true, MicronsPerPixel = 1, Background = false, BarColor = Colors.Red, Thickness = 10 };
        var box = OverlayRenderer.ScaleBarBounds(s, frame, 1)!.Value;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(frame));
            OverlayRenderer.Draw(dc, s, frame, 1);
        }
        var rtb = new RenderTargetBitmap(800, 400, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var px = new byte[4];
        // полоса линейки — нижние 10 px плашки до внутреннего отступа 8
        int x = (int)(box.X + box.Width / 2), y = (int)(box.Bottom - 8 - 5);
        rtb.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        Assert.True(px[2] > 200 && px[1] < 40 && px[0] < 40, $"BGRA = {px[0]},{px[1]},{px[2]},{px[3]}");
    }
}

public class MicronsPerPixelInputTests
{
    [Theory]
    [InlineData("0,25", 0.25)]
    [InlineData(" 0.5 ", 0.5)]
    [InlineData("1000", 1000)]
    [InlineData("0.0001", 0.0001)]
    public void Accepts(string text, double expected)
    {
        Assert.True(LumeneraViewer.ViewModels.ScalePanelViewModel.TryParseMicronsPerPixel(text, out var k));
        Assert.Equal(expected, k, 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1001")]
    [InlineData("0.00005")]
    [InlineData("мкм")]
    public void Rejects(string text) => Assert.False(LumeneraViewer.ViewModels.ScalePanelViewModel.TryParseMicronsPerPixel(text, out _));
}

public class UnitLanguageTests
{
    [Theory]
    [InlineData(200, LengthUnit.Micrometers, UnitLanguage.Russian, "200 мкм")]
    [InlineData(200, LengthUnit.Micrometers, UnitLanguage.English, "200 µm")]
    [InlineData(1500, LengthUnit.Micrometers, UnitLanguage.English, "1.5 mm")]
    [InlineData(500, LengthUnit.Millimeters, UnitLanguage.Russian, "0.5 мм")]
    [InlineData(500, LengthUnit.Millimeters, UnitLanguage.English, "0.5 mm")]
    public void FormatLength_InChosenLanguage(double microns, LengthUnit unit, UnitLanguage lang, string expected)
        => Assert.Equal(expected, ScaleBarMath.FormatLength(microns, unit, lang));

    [Fact]
    public void MeasurementLabel_English()
        => Assert.Equal("50 µm", MeasurementText.Label(new Measurement(new Point(0, 0), new Point(100, 0)), 0.5, UnitLanguage.English));

    [Fact]
    public void FromSettings_CarriesLanguage()
    {
        var a = new AppSettings();
        a.ScaleBar.UnitLanguage = UnitLanguage.English;
        Assert.Equal(UnitLanguage.English, OverlayState.FromSettings(a).UnitLanguage);
    }
}

public class OverlayColorListTests
{
    [Fact]
    public void HasPureBlue() => Assert.Contains(OverlayColors.All, c => c.Hex == "#0000FF" && c.Name == "Ярко-синий");

    [Fact]
    public void ColorsAreUnique() => Assert.Equal(OverlayColors.All.Count, OverlayColors.All.Select(c => c.Hex).Distinct().Count());
}
