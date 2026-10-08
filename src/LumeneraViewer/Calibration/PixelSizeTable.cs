namespace LumeneraViewer.ImageCalibration;

/// <summary>Откуда значение в таблице размера пикселя.</summary>
public enum PixelSizeSource { None, Calibrated, Derived }

/// <summary>Строка таблицы: увеличение, мкм/px (null — нечем пересчитать), источник; DerivedFrom — от какого увеличения пересчитано.</summary>
public sealed record PixelSizeRow(int Magnification, double? MicronsPerPixel, PixelSizeSource Source, int? DerivedFrom);

/// <summary>Чистая математика размера пикселя: таблица по увеличениям и пересчёт по ширине картинки.</summary>
public static class PixelSizeTable
{
    /// <summary>
    /// Строки по возрастанию увеличения. Откалиброванные (мкм/px &gt; 0) — как есть; остальные пересчитываются
    /// от ближайшего по увеличению откалиброванного (при равенстве — от большего): мкм/px₂ = мкм/px₁ × M₁ / M₂.
    /// </summary>
    public static IReadOnlyList<PixelSizeRow> Build(IEnumerable<(int Magnification, double? MicronsPerPixel)> profiles)
    {
        var list = profiles.Where(p => p.Magnification > 0).OrderBy(p => p.Magnification).ToList();
        var calibrated = list.Where(p => p.MicronsPerPixel is > 0).ToList();
        return list.Select(p =>
        {
            if (p.MicronsPerPixel is > 0 and var k) return new PixelSizeRow(p.Magnification, k, PixelSizeSource.Calibrated, null);
            if (calibrated.Count == 0) return new PixelSizeRow(p.Magnification, null, PixelSizeSource.None, null);
            var from = calibrated.OrderBy(c => Math.Abs(c.Magnification - p.Magnification)).ThenByDescending(c => c.Magnification).First();
            return new PixelSizeRow(p.Magnification, Derive(from.MicronsPerPixel!.Value, from.Magnification, p.Magnification),
                PixelSizeSource.Derived, from.Magnification);
        }).ToList();
    }

    /// <summary>мкм/px при увеличении to, если при from было micronsPerPixel (обратно пропорционально увеличению).</summary>
    public static double Derive(double micronsPerPixel, int fromMagnification, int toMagnification)
        => micronsPerPixel * fromMagnification / toMagnification;

    /// <summary>
    /// мкм на пиксель кадра камеры по мкм на пиксель картинки, если картинка уменьшена/увеличена:
    /// пиксель картинки = cameraWidth / imageWidth пикселей кадра.
    /// </summary>
    public static double ToCameraPixels(double imageMicronsPerPixel, int imageWidth, int cameraWidth)
        => imageWidth <= 0 || cameraWidth <= 0 ? imageMicronsPerPixel : imageMicronsPerPixel * imageWidth / cameraWidth;
}
