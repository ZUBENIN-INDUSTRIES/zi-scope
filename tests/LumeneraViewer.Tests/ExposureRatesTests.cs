using LumeneraViewer.Camera;
using Xunit;

namespace LumeneraViewer.Tests;

public class ExposureRatesTests
{
    // Как у INFINITY4-11 на USB 2.0: 2,75 к/с → потолок 364 мс; ниже частота — длиннее выдержка.
    private static readonly (float, float)[] Table = { (2.75f, 364f), (1.375f, 727f), (0.6875f, 1454f), (0.17f, 5880f) };

    [Fact]
    public void ShortExposure_TakesFastestRate() => Assert.Equal(2.75f, ExposureRates.Choose(Table, 40));

    [Fact]
    public void ExposureAtLimit_StaysOnFastestRate() => Assert.Equal(2.75f, ExposureRates.Choose(Table, 364));

    [Fact]
    public void LongExposure_TakesFastestRateThatAllowsIt()
    {
        Assert.Equal(1.375f, ExposureRates.Choose(Table, 500));
        Assert.Equal(0.6875f, ExposureRates.Choose(Table, 1000));
    }

    [Fact]
    public void BeyondEveryRate_TakesSlowest() => Assert.Equal(0.17f, ExposureRates.Choose(Table, 10000));

    [Fact]
    public void MaxExposure_IsLargestOverRates() => Assert.Equal(5880f, ExposureRates.MaxExposure(Table));

    [Fact]
    public void EmptyTable_Throws() => Assert.Throws<ArgumentException>(() => ExposureRates.Choose(Array.Empty<(float, float)>(), 10));
}
