using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class ScaleBarMathTests
{
    // Цели разобраны вручную: target = 0.2 · ширина · мкм/px, ближайшее из ряда 1-2-5×10ⁿ.
    [Theory]
    [InlineData(1.0, 2592, 500, "500 мкм")]     // цель 518.4 → 500
    [InlineData(0.5, 2592, 200, "200 мкм")]     // цель 259.2 → 200 (ближе, чем 500)
    [InlineData(0.1, 1000, 20, "20 мкм")]       // цель 20
    [InlineData(2.0, 2592, 1000, "1 мм")]       // цель 1036.8 → 1000
    [InlineData(3.6, 2592, 2000, "2 мм")]       // цель 1866.24 → 2000
    [InlineData(0.004, 1000, 1, "1 мкм")]       // цель 0.8 → 1
    [InlineData(0.0015, 1000, 0.2, "0.2 мкм")]  // цель 0.3 → 0.2
    public void Choose_PicksNearestRoundLength(double umPerPx, int width, double expectedMicrons, string expectedLabel)
    {
        var (microns, pixels, label) = ScaleBarMath.Choose(umPerPx, width);
        Assert.Equal(expectedMicrons, microns, 9);
        Assert.Equal(expectedMicrons / umPerPx, pixels, 6);
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void Choose_FixedLength_IsUsedAsIs()
    {
        var (microns, pixels, label) = ScaleBarMath.Choose(0.5, 2592, fixedMicrons: 250);
        Assert.Equal(250, microns);
        Assert.Equal(500, pixels, 6);
        Assert.Equal("250 мкм", label);
    }

    [Fact]
    public void Choose_InvalidScale_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScaleBarMath.Choose(0, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScaleBarMath.Choose(double.NaN, 1000));
    }

    [Theory]
    [InlineData(500, "500 мкм")]
    [InlineData(999, "999 мкм")]
    [InlineData(12.345, "12.35 мкм")]
    [InlineData(1000, "1 мм")]
    [InlineData(2500, "2.5 мм")]
    [InlineData(1234.5, "1.23 мм")]
    [InlineData(20000, "20 мм")]
    public void FormatLength_SwitchesToMillimetersAt1000(double microns, string expected)
        => Assert.Equal(expected, ScaleBarMath.FormatLength(microns));

    [Theory]
    [InlineData(500, LengthUnit.Millimeters, "0.5 мм")]
    [InlineData(20, LengthUnit.Millimeters, "0.02 мм")]
    [InlineData(2000, LengthUnit.Micrometers, "2 мм")]
    [InlineData(1, LengthUnit.Millimeters, "0.001 мм")]
    [InlineData(2, LengthUnit.Millimeters, "0.002 мм")]
    [InlineData(15, LengthUnit.Millimeters, "0.015 мм")]
    [InlineData(0.5, LengthUnit.Micrometers, "0.5 мкм")]
    [InlineData(0.004, LengthUnit.Micrometers, "0.004 мкм")]
    public void FormatLength_WithUnit(double microns, LengthUnit unit, string expected)
        => Assert.Equal(expected, ScaleBarMath.FormatLength(microns, unit));
}
