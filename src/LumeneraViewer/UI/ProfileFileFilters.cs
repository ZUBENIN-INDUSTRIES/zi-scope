using System.IO;
using LumeneraViewer.Settings;

namespace LumeneraViewer.UI;

/// <summary>Фильтры диалогов файлов и понятные тексты ошибок для профилей ZI-scope.</summary>
public static class ProfileFileFilters
{
    public static readonly string Color =
        $"Профиль цвета ZI-scope (*{ProfileFiles.ColorExtension})|*{ProfileFiles.ColorExtension}|Все файлы (*.*)|*.*";
    public static readonly string Config =
        $"Конфигурация ZI-scope (*{ProfileFiles.ConfigExtension})|*{ProfileFiles.ConfigExtension}|Все файлы (*.*)|*.*";

    /// <summary>Имя профиля → допустимое имя файла (недопустимые символы → «_»).</summary>
    public static string SafeName(string? name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var s = new string((name ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return s.Length == 0 ? "Профиль" : s;
    }

    /// <summary>Текст ошибки чтения или записи профиля для пользователя.</summary>
    public static string Describe(Exception ex) => ex switch
    {
        InvalidDataException => ProfileFiles.NotAProfileMessage + ".",
        FileNotFoundException or DirectoryNotFoundException => "Файл не найден.",
        UnauthorizedAccessException => "Нет доступа к файлу.",
        IOException io => "Ошибка доступа к файлу: " + io.Message,
        _ => ex.Message,
    };
}
