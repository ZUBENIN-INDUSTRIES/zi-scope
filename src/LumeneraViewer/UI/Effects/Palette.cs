using System.Windows.Media;

namespace LumeneraViewer.UI.Effects;

/// <summary>Палитра reactbits из спецификации.</summary>
public static class Palette
{
    public static readonly Color Background = Color.FromRgb(0x0B, 0x0B, 0x12);
    public static readonly Color Violet = Color.FromRgb(0x7C, 0x3A, 0xED);
    public static readonly Color Cyan = Color.FromRgb(0x06, 0xB6, 0xD4);
    public static readonly Color Pink = Color.FromRgb(0xF4, 0x72, 0xB6);

    public static Color WithAlpha(Color c, double a) => Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0, 1) * 255), c.R, c.G, c.B);

    public static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
