using LumeneraViewer.Imaging;

namespace LumeneraViewer.Export;

/// <summary>Параметры съёмки для файла `.json` рядом со снимком. Размеры и формат дописывает экспорт.</summary>
public sealed record CaptureMetadata(
    DateTime Time,
    string? CameraModel,
    string? CameraSerial,
    bool IsSimulated,
    float? ExposureMs,
    float? Gain,
    ImageAdjustments Adjustments,
    string? Objective,
    double? MicronsPerPixel);
