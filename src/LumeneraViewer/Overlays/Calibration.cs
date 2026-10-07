using System.Windows;

namespace LumeneraViewer.Overlays;

/// <summary>Калибровка объектива по объект-микрометру.</summary>
public static class Calibration
{
    /// <summary>мкм на пиксель кадра: известная длина отрезка a–b, делённая на его длину в пикселях.</summary>
    public static double MicronsPerPixel(Point a, Point b, double knownMicrons)
    {
        var px = (b - a).Length;
        if (!(px > 0.5)) throw new ArgumentException("Отрезок слишком короткий — проведите линию по известному отрезку.");
        if (!(knownMicrons > 0) || double.IsInfinity(knownMicrons)) throw new ArgumentException("Известная длина должна быть больше нуля.", nameof(knownMicrons));
        return knownMicrons / px;
    }
}
