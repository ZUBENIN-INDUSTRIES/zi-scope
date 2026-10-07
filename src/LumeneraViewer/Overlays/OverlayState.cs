using System.Windows;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Overlays;

/// <summary>
/// Снимок всего, что рисуется поверх кадра. Неизменяемый: его можно передать в фоновый поток
/// (впечатывание в файл). Все координаты — в пикселях кадра полного разрешения.
/// </summary>
public sealed record OverlayState
{
    /// <summary>мкм на пиксель текущего объектива; null — объектив не откалиброван (плашка не рисуется, длины в px).</summary>
    public double? MicronsPerPixel { get; init; }

    // ── Плашка масштаба (рисуется только при ScaleBarEnabled и MicronsPerPixel > 0) ──
    public bool ScaleBarEnabled { get; init; }
    public ScaleBarCorner Corner { get; init; } = ScaleBarCorner.BottomRight;
    public OverlayColor Color { get; init; } = OverlayColor.White;
    public bool Background { get; init; } = true;
    /// <summary>Размер шрифта при scale = 1.</summary>
    public double FontSize { get; init; } = 16;
    /// <summary>Толщина линии при scale = 1.</summary>
    public double Thickness { get; init; } = 4;
    /// <summary>Длина вручную, мкм; null — «Авто».</summary>
    public double? FixedMicrons { get; init; }
    public LengthUnit Unit { get; init; } = LengthUnit.Micrometers;
    /// <summary>Левый верхний угол плашки в долях кадра (0..1); null — в углу Corner.</summary>
    public Point? CustomPosition { get; init; }

    // ── Измерения и направляющие ──
    public IReadOnlyList<Measurement> Measurements { get; init; } = Array.Empty<Measurement>();
    /// <summary>Линия, которую сейчас проводят (линейка/калибровка). Только для превью.</summary>
    public Measurement? PendingLine { get; init; }
    public bool Crosshair { get; init; }
    public bool Grid { get; init; }
    /// <summary>Шаг сетки при калибровке, мкм.</summary>
    public double GridStepMicrons { get; init; } = 100;
    /// <summary>Шаг сетки без калибровки, % ширины кадра.</summary>
    public double GridStepPercent { get; init; } = 10;

    public bool IsCalibrated => MicronsPerPixel is > 0;
    public bool ShowsScaleBar => ScaleBarEnabled && IsCalibrated;

    /// <summary>Нечего рисовать (экспорт может не впечатывать пустой оверлей).</summary>
    public bool IsEmpty => !ShowsScaleBar && Measurements.Count == 0 && PendingLine is null && !Crosshair && !Grid;

    /// <summary>Мкм/px текущего объектива из настроек (null — не откалиброван или профиль не найден).</summary>
    public static double? CurrentMicronsPerPixel(AppSettings s)
    {
        var p = s.Objectives?.FirstOrDefault(o => o.Name == s.CurrentObjective) ?? s.Objectives?.FirstOrDefault();
        return p?.MicronsPerPixel is > 0 and var k ? k : null;
    }

    /// <summary>Состояние из настроек (плашка, сетка, перекрестие, текущий объектив) и списка измерений.</summary>
    public static OverlayState FromSettings(AppSettings s, IEnumerable<Measurement>? measurements = null)
    {
        var sb = s.ScaleBar ?? new ScaleBarSettings();
        var g = s.Guides ?? new GuideSettings();
        return new OverlayState
        {
            MicronsPerPixel = CurrentMicronsPerPixel(s),
            ScaleBarEnabled = sb.Enabled,
            Corner = sb.Corner,
            Color = sb.Color,
            Background = sb.Background,
            FontSize = sb.FontSize,
            Thickness = sb.Thickness,
            FixedMicrons = sb.FixedMicrons is > 0 ? sb.FixedMicrons : null,
            Unit = sb.Unit,
            CustomPosition = sb.CustomX is { } x && sb.CustomY is { } y ? new Point(x, y) : null,
            Measurements = measurements?.ToArray() ?? Array.Empty<Measurement>(),
            Crosshair = g.Crosshair,
            Grid = g.Grid,
            GridStepMicrons = g.GridStepMicrons,
            GridStepPercent = g.GridStepPercent,
        };
    }
}
