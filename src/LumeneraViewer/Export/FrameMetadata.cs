using System.Text.Json.Serialization;
using LumeneraViewer.Imaging;

namespace LumeneraViewer.Export;

/// <summary>
/// Метаданные записанного кадра: состав .json обычного снимка (имена и порядок свойств — формат файла)
/// и запись кадра в списке <c>Frames</c> проекта серии. <see cref="File"/> — имя файла без папки.
/// </summary>
public sealed record FrameMetadata(
    DateTime Time, string File, ImageFormatKind Format, int Width, int Height,
    string? CameraModel, string? CameraSerial, bool IsSimulated, float? ExposureMs, float? Gain,
    ImageAdjustments Adjustments, string? Objective, double? MicronsPerPixel, bool OverlaysBurned, int? SeriesIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Extra = null);
