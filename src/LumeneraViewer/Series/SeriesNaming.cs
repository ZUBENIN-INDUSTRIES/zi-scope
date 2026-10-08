using System.Globalization;
using System.IO;
using System.Text;

namespace LumeneraViewer.Series;

/// <summary>Имена файлов серии по шаблону с токенами {group} {sample} {mag} {kind} {series}.</summary>
public static class SeriesNaming
{
    private static readonly HashSet<char> Invalid = new(Path.GetInvalidFileNameChars());

    /// <summary>
    /// Имя файла (без папки): шаблон плана с подставленными номерами, недопустимые символы → «_».
    /// <paramref name="extension"/> — с точкой или без. Пустой шаблон — шаблон по умолчанию.
    /// </summary>
    public static string FileName(SeriesPlan p, SeriesStepKey k, FrameKind kind, string extension)
    {
        var template = string.IsNullOrWhiteSpace(p.FileNameTemplate) ? SeriesPlan.DefaultTemplate : p.FileNameTemplate.Trim();
        var inv = CultureInfo.InvariantCulture;
        var stem = new StringBuilder(template)
            .Replace("{group}", k.Group.ToString(inv))
            .Replace("{sample}", k.Sample.ToString(inv))
            .Replace("{mag}", k.Magnification.ToString(inv))
            .Replace("{kind}", kind.Number.ToString(inv))
            .Replace("{series}", (p.Name ?? "").Trim())
            .ToString();

        var chars = stem.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (Invalid.Contains(chars[i])) chars[i] = '_';
        stem = new string(chars).TrimEnd('.', ' ');   // Windows не любит точку и пробел в конце имени
        if (stem.Length == 0) stem = "_";

        var ext = (extension ?? "").Trim();
        if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
        return stem + ext;
    }
}
