using System.Windows;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class OverlayStateTests
{
    /// <summary>Текущее увеличение ×100 с калибровкой <paramref name="mpp"/>.</summary>
    private static AppSettings Settings(double? mpp)
    {
        var s = new AppSettings { CurrentMagnification = 100 };
        MagnificationSettings.GetOrCreate(s, 100).MicronsPerPixel = mpp;
        return s;
    }

    [Fact]
    public void CustomPosition_OnlyWhenBothXAndYSet()
    {
        var s = Settings(0.5);
        s.ScaleBar.CustomX = 0.25;
        s.ScaleBar.CustomY = 0.75;
        Assert.Equal(new Point(0.25, 0.75), OverlayState.FromSettings(s).CustomPosition);

        s.ScaleBar.CustomY = null;
        Assert.Null(OverlayState.FromSettings(s).CustomPosition);

        s.ScaleBar.CustomX = null;
        s.ScaleBar.CustomY = 0.3;
        Assert.Null(OverlayState.FromSettings(s).CustomPosition);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void FixedMicrons_NonPositive_IsAuto(double fixedMicrons)
    {
        var s = Settings(0.5);
        s.ScaleBar.FixedMicrons = fixedMicrons;
        Assert.Null(OverlayState.FromSettings(s).FixedMicrons);
    }

    [Fact]
    public void FixedMicrons_Positive_IsKept()
    {
        var s = Settings(0.5);
        s.ScaleBar.FixedMicrons = 250;
        Assert.Equal(250, OverlayState.FromSettings(s).FixedMicrons);
    }

    [Fact]
    public void MicronsPerPixel_FromCurrentMagnification_NotFromObjectives()
    {
        var s = Settings(0.5);
        MagnificationSettings.GetOrCreate(s, 200).MicronsPerPixel = 0.25;
        s.Objectives = new() { new ObjectiveProfile("10x", 3.0) };
        s.CurrentObjective = "10x";
        Assert.Equal(0.5, OverlayState.CurrentMicronsPerPixel(s));
        s.CurrentMagnification = 200;
        Assert.Equal(0.25, OverlayState.FromSettings(s).MicronsPerPixel);
    }

    [Fact]
    public void MicronsPerPixel_UnknownMagnification_IsNull()
    {
        var s = Settings(0.5);
        s.CurrentMagnification = 1000;
        Assert.Null(OverlayState.CurrentMicronsPerPixel(s));
    }

    [Fact]
    public void MicronsPerPixel_Uncalibrated_IsNull_AndScaleBarHidden()
    {
        var s = Settings(null);
        var state = OverlayState.FromSettings(s);
        Assert.Null(state.MicronsPerPixel);
        Assert.True(state.ScaleBarEnabled);   // настройка включена по умолчанию…
        Assert.False(state.ShowsScaleBar);    // …но без калибровки плашка не рисуется
    }

    [Fact]
    public void Measurements_AndGuides_AreCopied()
    {
        var s = Settings(0.5);
        s.Guides.Grid = true;
        s.Guides.GridStepMicrons = 50;
        var m = new Measurement(new Point(0, 0), new Point(10, 0));
        var state = OverlayState.FromSettings(s, new[] { m });
        Assert.Equal(new[] { m }, state.Measurements);
        Assert.True(state.Grid);
        Assert.Equal(50, state.GridStepMicrons);
        Assert.Null(state.PendingLine);
    }
}
