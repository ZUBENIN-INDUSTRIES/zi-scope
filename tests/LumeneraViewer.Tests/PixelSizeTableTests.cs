using LumeneraViewer.ImageCalibration;

namespace LumeneraViewer.Tests;

/// <summary>G04/G05: таблица мкм/px по увеличениям и пересчёт по ширине картинки.</summary>
public sealed class PixelSizeTableTests
{
    [Fact]
    public void Build_DerivesFromNearestCalibrated_InverselyToMagnification()
    {
        // ×100 = 1.0 мкм/px; ×400 = 0.3 (откалибровано «неидеально» — значит, ближайший важен)
        var rows = PixelSizeTable.Build(new (int, double?)[] { (50, null), (100, 1.0), (200, null), (300, null), (400, 0.3), (1000, null) });

        Assert.Equal(new[] { 50, 100, 200, 300, 400, 1000 }, rows.Select(r => r.Magnification));
        Assert.Equal(2.0, rows[0].MicronsPerPixel!.Value, 9);           // от ×100: 1.0·100/50
        Assert.Equal(PixelSizeSource.Derived, rows[0].Source);
        Assert.Equal(100, rows[0].DerivedFrom);
        Assert.Equal(PixelSizeSource.Calibrated, rows[1].Source);
        Assert.Equal(1.0, rows[1].MicronsPerPixel!.Value, 9);
        Assert.Equal(0.5, rows[2].MicronsPerPixel!.Value, 9);           // ×200: до ×100 ближе (100 < 200)
        Assert.Equal(0.4, rows[3].MicronsPerPixel!.Value, 9);           // ×300: ×400 ближе → 0.3·400/300
        Assert.Equal(400, rows[3].DerivedFrom);
        Assert.Equal(0.12, rows[5].MicronsPerPixel!.Value, 9);          // ×1000 от ×400: 0.3·400/1000
    }

    [Fact]
    public void Build_NothingCalibrated_AllEmpty()
    {
        var rows = PixelSizeTable.Build(new (int, double?)[] { (200, null), (100, 0.0) });

        Assert.All(rows, r => { Assert.Null(r.MicronsPerPixel); Assert.Equal(PixelSizeSource.None, r.Source); });
        Assert.Equal(100, rows[0].Magnification);   // по возрастанию
    }

    [Fact]
    public void Derive_Formula() => Assert.Equal(0.25, PixelSizeTable.Derive(1.0, 100, 400), 9);

    [Theory]
    [InlineData(2.0, 1296, 2592, 1.0)]     // картинка вдвое меньше кадра: пиксель картинки = 2 пикселя кадра
    [InlineData(0.5, 2592, 1296, 1.0)]
    [InlineData(0.8, 1600, 1600, 0.8)]
    public void ToCameraPixels_ScalesByWidthRatio(double imageUmPerPx, int imageWidth, int cameraWidth, double expected)
        => Assert.Equal(expected, PixelSizeTable.ToCameraPixels(imageUmPerPx, imageWidth, cameraWidth), 9);
}
