using System.Text.Json.Serialization;

namespace LumeneraViewer.Imaging;

/// <summary>
/// Программная коррекция. Brightness, Contrast, Temperature, Tint: −100…100; Gamma &gt; 0 (1 — без изменений);
/// Saturation: 0…200 (100 — без изменений).
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

    [JsonIgnore]
    public bool IsIdentity => this == Default;
}
