using System.Globalization;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Overlays;

/// <summary>Подбор «круглой» длины плашки масштаба и её подпись.</summary>
public static class ScaleBarMath
{
    private static readonly double[] Mantissas = { 1, 2, 5 };

    /// <summary>
    /// Длина из ряда 1-2-5×10ⁿ мкм, ближайшая к targetFraction·frameWidth пикселей;
    /// при fixedMicrons — ровно она.
    /// </summary>
    public static (double lengthMicrons, double lengthPixels, string label) Choose(double micronsPerPixel, int frameWidth, double targetFraction = 0.2, double? fixedMicrons = null)
    {
        if (!(micronsPerPixel > 0) || double.IsInfinity(micronsPerPixel))
            throw new ArgumentOutOfRangeException(nameof(micronsPerPixel), "Масштаб должен быть положительным.");

        double microns;
        if (fixedMicrons is > 0 and var f) microns = f;
        else
        {
            var target = Math.Max(1, frameWidth) * targetFraction * micronsPerPixel;
            var decade = (int)Math.Floor(Math.Log10(target));
            microns = double.NaN;
            var best = double.MaxValue;
            for (var n = decade - 1; n <= decade + 1; n++)
                foreach (var m in Mantissas)
                {
                    var v = n >= 0 ? m * Math.Pow(10, n) : m / Math.Pow(10, -n);
                    var d = Math.Abs(v - target);
                    if (d < best) { best = d; microns = v; }
                }
        }
        return (microns, microns / micronsPerPixel, FormatLength(microns));
    }

    /// <summary>&lt; 1000 мкм → «N мкм», иначе «N мм»; до 2 знаков после точки (меньше 1 — 2 значащие цифры), без хвостовых нулей.</summary>
    public static string FormatLength(double microns) => FormatLength(microns, LengthUnit.Micrometers);

    /// <summary>Micrometers — автоматически мкм/мм (как FormatLength); Millimeters — всегда мм.</summary>
    public static string FormatLength(double microns, LengthUnit unit)
    {
        if (unit == LengthUnit.Millimeters || Math.Abs(microns) >= 1000)
            return Num(microns / 1000) + " мм";
        return Num(microns) + " мкм";
    }

    private static string Num(double v) => Number(v, 2);

    /// <summary>
    /// Число с decimals знаками после точки без хвостовых нулей; значения меньше 1 по модулю
    /// получают минимум 2 значащие цифры — подпись не округляется до нуля.
    /// </summary>
    internal static string Number(double v, int decimals)
    {
        var a = Math.Abs(v);
        if (a > 0 && a < 1) decimals = Math.Clamp(1 - (int)Math.Floor(Math.Log10(a)), decimals, 12);
        return Math.Round(v, decimals, MidpointRounding.AwayFromZero).ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
    }
}
