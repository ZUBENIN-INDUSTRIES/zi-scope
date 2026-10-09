using System.Globalization;
using System.Text.RegularExpressions;

namespace LumeneraViewer.ImageCalibration;

/// <summary>Единицы подписи масштабной линии.</summary>
public enum ScaleUnit { Micrometers, Millimeters }

/// <summary>Число и единицы с подписи; Microns — в микрометрах.</summary>
public sealed record ScaleReading(double Value, ScaleUnit Unit)
{
    public double Microns => Unit == ScaleUnit.Millimeters ? Value * 1000 : Value;

    /// <summary>Как прочитано: «100 µm», «0.5 mm» — независимо от того, как OCR написал единицы.</summary>
    public string Display => Value.ToString("0.###", CultureInfo.InvariantCulture) + (Unit == ScaleUnit.Millimeters ? " mm" : " µm");
}

/// <summary>
/// Разбор подписи масштаба («200 µm», «0,5 мм», «50um», «100 мкм»): первое число с единицами
/// мкм/µm/μm/um/мм/mm, запятая или точка. Без единиц, без числа или ноль — null.
/// </summary>
public static partial class ScaleTextParser
{
    // Между µ и m OCR иногда ставит пробел; «мкм» проверяется раньше «мм».
    // OCR Windows читает «µm» как «pm», а с русским языком — как «рт» (кириллица): это тоже микрометры.
    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(мк\s*м|[µμuрp]\s*[mтм]|мм|mm)(?![a-zа-я])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static ScaleReading? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (Match m in Pattern().Matches(text))
        {
            if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !(v > 0))
                continue;
            var unit = m.Groups[2].Value.ToLowerInvariant().Replace(" ", "") is "мм" or "mm" ? ScaleUnit.Millimeters : ScaleUnit.Micrometers;
            return new ScaleReading(v, unit);
        }
        return null;
    }
}
