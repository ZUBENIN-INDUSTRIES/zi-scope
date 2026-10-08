using LumeneraViewer.ImageCalibration;

namespace LumeneraViewer.Tests;

/// <summary>Разбор подписи масштабной линии (результат OCR) в микрометры.</summary>
public sealed class ScaleTextParserTests
{
    [Theory]
    [InlineData("200 µm", 200.0, ScaleUnit.Micrometers)]
    [InlineData("200 μm", 200.0, ScaleUnit.Micrometers)]   // греческая «мю»
    [InlineData("0,5 мм", 0.5, ScaleUnit.Millimeters)]
    [InlineData("50um", 50.0, ScaleUnit.Micrometers)]
    [InlineData("100 мкм", 100.0, ScaleUnit.Micrometers)]
    [InlineData("1.5 mm", 1.5, ScaleUnit.Millimeters)]
    [InlineData("Scale: 20 UM", 20.0, ScaleUnit.Micrometers)]
    [InlineData("100 µ m", 100.0, ScaleUnit.Micrometers)]
    // Так «100 µm» прочитал живой Windows OCR с русским языком (ручная проверка): µ → р, m → т (кириллица)
    [InlineData("100 рт | о 100 рт", 100.0, ScaleUnit.Micrometers)]
    [InlineData("100 pm", 100.0, ScaleUnit.Micrometers)]     // английский OCR путает µ и p
    public void Parse_NumberAndUnit(string text, double value, ScaleUnit unit)
    {
        var r = ScaleTextParser.Parse(text);

        Assert.NotNull(r);
        Assert.Equal(value, r!.Value, 9);
        Assert.Equal(unit, r.Unit);
    }

    [Theory]
    [InlineData("200 µm", 200.0)]
    [InlineData("0,5 мм", 500.0)]
    [InlineData("2 mm", 2000.0)]
    public void Microns_ConvertsUnits(string text, double microns)
        => Assert.Equal(microns, ScaleTextParser.Parse(text)!.Microns, 9);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("200")]          // без единиц — неизвестно, что это
    [InlineData("µm")]           // без числа
    [InlineData("0 µm")]         // ноль — не масштаб
    [InlineData("12 cm")]
    public void Parse_Garbage_ReturnsNull(string? text) => Assert.Null(ScaleTextParser.Parse(text));
}
