using System.IO;
using System.Text;

namespace LumeneraViewer.Settings;

/// <summary>
/// Атомарная запись файла: временный файл рядом (<c>путь.tmp</c>) → <c>Flush(true)</c> на диск → переименование
/// поверх. Частичных файлов не остаётся: при сбое старый файл цел, временный удаляется, исключение пробрасывается.
/// </summary>
public static class AtomicFile
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    /// <param name="overwrite">false и файл уже есть (или появился за время записи) — <see cref="IOException"/>, файл не тронут.</param>
    public static void Write(string path, Action<Stream> write, bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);
        var tmp = path + ".tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write(fs);
                fs.Flush(true);
            }
            File.Move(tmp, path, overwrite);
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* временный файл мог не создаться */ }
            throw;
        }
    }

    /// <summary>Текст в UTF-8 без BOM (как <see cref="File.WriteAllText(string, string?)"/>), атомарно.</summary>
    public static void WriteAllText(string path, string text, bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Utf8NoBom.GetBytes(text);
        Write(path, s => s.Write(bytes), overwrite);
    }
}
