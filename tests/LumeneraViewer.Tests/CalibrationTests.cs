using System.Windows;
using LumeneraViewer.Overlays;

namespace LumeneraViewer.Tests;

public sealed class CalibrationTests
{
    [Fact]
    public void MicronsPerPixel_KnownLengthOverPixelDistance()
    {
        // Отрезок 3-4-5: 500 px, известная длина 100 мкм → 0.2 мкм/px.
        Assert.Equal(0.2, Calibration.MicronsPerPixel(new Point(10, 10), new Point(310, 410), 100), 12);
        Assert.Equal(2.5, Calibration.MicronsPerPixel(new Point(0, 0), new Point(400, 0), 1000), 12);
    }

    [Fact]
    public void MicronsPerPixel_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => Calibration.MicronsPerPixel(new Point(5, 5), new Point(5, 5), 100));
        Assert.Throws<ArgumentException>(() => Calibration.MicronsPerPixel(new Point(0, 0), new Point(10, 0), 0));
        Assert.Throws<ArgumentException>(() => Calibration.MicronsPerPixel(new Point(0, 0), new Point(10, 0), -5));
    }
}

public sealed class MeasurementTextTests
{
    [Theory]
    [InlineData(0, 0, 300, 400, null, "500 px")]
    [InlineData(0, 0, 10.25, 0, null, "10.3 px")]
    [InlineData(0, 0, 300, 400, 0.5, "250 мкм")]
    [InlineData(0, 0, 300, 400, 4.0, "2 мм")]
    [InlineData(0, 0, 100, 0, 0.1234, "12.34 мкм")]
    [InlineData(0, 0, 0.04, 0, null, "0.04 px")]
    [InlineData(0, 0, 1, 0, 0.002, "0.002 мкм")]
    public void Label_UsesMicronsWhenCalibrated_PixelsOtherwise(double ax, double ay, double bx, double by, double? umPerPx, string expected)
        => Assert.Equal(expected, MeasurementText.Label(new Measurement(new Point(ax, ay), new Point(bx, by)), umPerPx));
}
