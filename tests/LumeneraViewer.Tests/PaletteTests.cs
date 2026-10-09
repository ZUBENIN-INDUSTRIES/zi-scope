using System.Windows.Media;
using LumeneraViewer.UI.Effects;
using Xunit;

namespace LumeneraViewer.Tests;

/// <summary>Палитра — строго по истории 34 спецификации (тёмная тема Google Gemini).</summary>
public class PaletteTests
{
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    [Theory]
    [InlineData(nameof(Palette.Window), "#131314")]
    [InlineData(nameof(Palette.Panel), "#1E1F20")]
    [InlineData(nameof(Palette.Raised), "#282A2C")]
    [InlineData(nameof(Palette.RaisedHover), "#333537")]
    [InlineData(nameof(Palette.Input), "#1E1F20")]
    [InlineData(nameof(Palette.Border), "#444746")]
    [InlineData(nameof(Palette.Text), "#E3E3E3")]
    [InlineData(nameof(Palette.TextSecondary), "#C4C7C5")]
    [InlineData(nameof(Palette.TextDisabled), "#8E918F")]
    [InlineData(nameof(Palette.Accent), "#A8C7FA")]
    [InlineData(nameof(Palette.OnAccent), "#062E6F")]
    [InlineData(nameof(Palette.Success), "#6DD58C")]
    [InlineData(nameof(Palette.Warning), "#FDD663")]
    [InlineData(nameof(Palette.Error), "#F2B8B5")]
    public void Цвет_по_истории_34(string name, string expected)
    {
        var c = (Color)typeof(Palette).GetField(name)!.GetValue(null)!;
        Assert.Equal(expected, Hex(c));
        Assert.Equal(255, c.A);
    }

    // Относительная яркость и контраст по WCAG 2.x (формула из стандарта, не из кода программы).
    private static double Lum(Color c)
    {
        static double Ch(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        double l1 = Lum(a), l2 = Lum(b);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    [Theory]
    [InlineData(nameof(Palette.Text), nameof(Palette.Panel))]
    [InlineData(nameof(Palette.Text), nameof(Palette.Window))]
    [InlineData(nameof(Palette.TextSecondary), nameof(Palette.Panel))]
    [InlineData(nameof(Palette.OnAccent), nameof(Palette.Accent))]
    [InlineData(nameof(Palette.Window), nameof(Palette.Warning))]
    [InlineData(nameof(Palette.Window), nameof(Palette.Error))]
    public void Контраст_текста_WCAG_AA(string fore, string back)
    {
        Color Get(string n) => (Color)typeof(Palette).GetField(n)!.GetValue(null)!;
        Assert.True(Contrast(Get(fore), Get(back)) >= 4.5, $"{fore} на {back}: {Contrast(Get(fore), Get(back)):0.00}:1");
    }

    // Недоступный текст и акцент на панели. Оба проходят обычный порог AA 4.5:1 (не только 3:1 для крупного).
    [Theory]
    [InlineData(nameof(Palette.TextDisabled), nameof(Palette.Panel), 4.5)]
    [InlineData(nameof(Palette.Accent), nameof(Palette.Panel), 4.5)]
    public void Контраст_вспомогательного_и_акцента(string fore, string back, double min)
    {
        Color Get(string n) => (Color)typeof(Palette).GetField(n)!.GetValue(null)!;
        var c = Contrast(Get(fore), Get(back));
        Assert.True(c >= min, $"{fore} на {back}: {c:0.00}:1, нужно {min}:1");
    }

    [Fact]
    public void Выделение_это_акцент_с_альфой_016()
    {
        Assert.Equal("#A8C7FA", Hex(Palette.AccentSelection));
        Assert.Equal(41, Palette.AccentSelection.A); // 0.16 × 255 ≈ 41
    }
}
