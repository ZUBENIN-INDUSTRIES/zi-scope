using System.Windows;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Overlays;

/// <summary>Отрезок в координатах кадра (пиксели полного разрешения).</summary>
public sealed record Measurement(Point A, Point B)
{
    public double LengthPixels => (B - A).Length;
}

public static class MeasurementText
{
    /// <summary>При калибровке — «N мкм»/«N мм», без неё — «N px» (пиксели за микроны не выдаются).</summary>
    public static string Label(Measurement m, double? umPerPx, UnitLanguage language = UnitLanguage.Russian)
    {
        var px = m.LengthPixels;
        if (umPerPx is > 0 and var k) return ScaleBarMath.FormatLength(px * k, LengthUnit.Micrometers, language);
        return ScaleBarMath.Number(px, 1) + " px";
    }
}
