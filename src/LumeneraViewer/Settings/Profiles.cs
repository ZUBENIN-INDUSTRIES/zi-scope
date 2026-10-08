using LumeneraViewer.Imaging;
using LumeneraViewer.Series;

namespace LumeneraViewer.Settings;

/// <summary>Всё, что запоминается для одного увеличения микроскопа: калибровка, длина плашки, цвет, экспозиция.</summary>
public sealed class MagnificationProfile
{
    public int Magnification { get; set; }
    public double? MicronsPerPixel { get; set; }
    /// <summary>Длина плашки масштаба, мкм; null — «Авто».</summary>
    public double? ScaleBarMicrons { get; set; }
    public ImageAdjustments Adjustments { get; set; } = ImageAdjustments.Default;
    public float? ExposureMs { get; set; }
    public float? Gain { get; set; }
}

/// <summary>Именованный профиль цвета (основные и глубокие настройки, по флажку — экспозиция и усиление).</summary>
public sealed class ColorProfile
{
    public string Name { get; set; } = "";
    public ImageAdjustments Adjustments { get; set; } = ImageAdjustments.Default;
    public bool IncludesExposure { get; set; }
    public float? ExposureMs { get; set; }
    public float? Gain { get; set; }
}

/// <summary>Последние параметры конструктора серии — подставляются в «Новая серия».</summary>
public sealed class SeriesDefaults
{
    public int Groups { get; set; } = 16;
    public int SamplesPerGroup { get; set; } = 4;
    public List<int> Magnifications { get; set; } = new() { 100, 200 };
    public List<FrameKind> Kinds { get; set; } = SeriesPlan.DefaultKinds.ToList();
    public SeriesOrder Order { get; set; } = SeriesOrder.SampleBySample;
    public string FileNameTemplate { get; set; } = SeriesPlan.DefaultTemplate;
}
