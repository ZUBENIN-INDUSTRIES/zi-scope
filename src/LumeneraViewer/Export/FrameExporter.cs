using System.Globalization;
using System.IO;
using System.Text;

namespace LumeneraViewer.Export;

/// <summary>
/// Запись кадров в файлы. Кодирование и запись идут на собственном STA-потоке
/// (энкодерам и RenderTargetBitmap нужен STA), по одной операции за раз —
/// повторные нажатия встают в очередь и не портят файлы.
/// </summary>
public sealed partial class FrameExporter
{
    public const string DefaultPrefix = "Lumenera";

    /// <summary>{prefix}_{yyyyMMdd_HHmmss_fff}[_{objective}][_{0001}].{ext}; недопустимые символы → «_».</summary>
    public static string BuildFileName(string prefix, DateTime t, string? objective, int? seriesIndex, ImageFormatKind f)
    {
        var p = string.IsNullOrWhiteSpace(prefix) ? DefaultPrefix : prefix.Trim();
        var sb = new StringBuilder(Sanitize(p));
        sb.Append('_').Append(t.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(objective)) sb.Append('_').Append(Sanitize(objective.Trim()));
        if (seriesIndex is { } i) sb.Append('_').Append(i.ToString("0000", CultureInfo.InvariantCulture));
        return sb.Append(Extension(f)).ToString();
    }

    public static string Extension(ImageFormatKind f) => f switch
    {
        ImageFormatKind.Png => ".png",
        ImageFormatKind.Jpeg => ".jpg",
        ImageFormatKind.Tiff => ".tif",
        ImageFormatKind.Bmp => ".bmp",
        ImageFormatKind.JpegXr => ".jxr",
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    /// <summary>Расширения, которые показывает лента (в нижнем регистре, с точкой).</summary>
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".jxr", ".wdp" };

    private static readonly HashSet<char> Invalid = new(Path.GetInvalidFileNameChars());

    private static string Sanitize(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (Invalid.Contains(chars[i])) chars[i] = '_';
        return new string(chars);
    }
}
