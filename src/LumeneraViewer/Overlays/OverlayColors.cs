using System.Globalization;
using System.Windows.Media;

namespace LumeneraViewer.Overlays;

/// <summary>
/// Цвета для элементов на самом снимке (плашка масштаба). Это не цвета интерфейса — они впечатываются в кадр,
/// поэтому заданы здесь, а не в Palette. Хранятся в настройках как «#RRGGBB».
/// </summary>
public static class OverlayColors
{
    public sealed record Named(string Name, Color Color)
    {
        public SolidColorBrush Brush { get; } = Freeze(new SolidColorBrush(Color));
        public string Hex => ToHex(Color);
    }

    public static IReadOnlyList<Named> All { get; } = new Named[]
    {
        new("Белый", Colors.White),
        new("Чёрный", Colors.Black),
        new("Жёлтый", Color.FromRgb(0xFA, 0xCC, 0x15)),
        new("Оранжевый", Color.FromRgb(0xF9, 0x73, 0x16)),
        new("Красный", Color.FromRgb(0xEF, 0x44, 0x44)),
        new("Пурпурный", Color.FromRgb(0xD9, 0x46, 0xEF)),
        new("Синий", Color.FromRgb(0x3B, 0x82, 0xF6)),
        new("Ярко-синий", Color.FromRgb(0x00, 0x00, 0xFF)),
        new("Голубой", Color.FromRgb(0x22, 0xD3, 0xEE)),
        new("Зелёный", Color.FromRgb(0x22, 0xC5, 0x5E)),
        new("Серый", Color.FromRgb(0x9C, 0xA3, 0xAF)),
        new("Тёмно-серый", Color.FromRgb(0x37, 0x41, 0x51)),
    };

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>«#RRGGBB» или «RRGGBB» → цвет; иначе null.</summary>
    public static Color? Parse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var h = hex.Trim().TrimStart('#');
        if (h.Length != 6 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return null;
        return Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    /// <summary>Индекс цвета в <see cref="All"/>; -1 — нет такого.</summary>
    public static int IndexOf(Color? c) => c is { } x ? All.ToList().FindIndex(n => n.Color.R == x.R && n.Color.G == x.G && n.Color.B == x.B) : -1;

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
