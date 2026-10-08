using System.Text.Json.Serialization;

namespace LumeneraViewer.Imaging;

/// <summary>
/// Программная коррекция. Brightness, Contrast, Temperature, Tint: −100…100; Gamma &gt; 0 (1 — без изменений);
/// Saturation: 0…200 (100 — без изменений). Deep — глубокая коррекция (окно «Глубокие настройки цвета»).
/// </summary>
public sealed record ImageAdjustments(
    double Brightness = 0,
    double Contrast = 0,
    double Gamma = 1,
    double Saturation = 100,
    double Temperature = 0,
    double Tint = 0,
    bool Grayscale = false,
    bool Negative = false)
{
    public static ImageAdjustments Default { get; } = new();

    /// <summary>Глубокая коррекция; в старом JSON поля нет — берётся нейтраль, null тоже читается как нейтраль.</summary>
    public DeepColor Deep { get => _deep; init => _deep = value ?? DeepColor.Neutral; }

    private readonly DeepColor _deep = DeepColor.Neutral;

    /// <summary>Основные поля по умолчанию (без учёта Deep).</summary>
    [JsonIgnore]
    internal bool IsBaseIdentity => this with { Deep = DeepColor.Neutral } == Default;

    [JsonIgnore]
    public bool IsIdentity => IsBaseIdentity && Deep.IsNeutral;
}
