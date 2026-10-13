using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LumeneraViewer.Export;

namespace LumeneraViewer.Series;

/// <summary>
/// Проект серии — файл <c>&lt;имя серии&gt;.zisc</c> в папке серии: план, текущий шаг, статусы шагов и одним
/// списком <c>Frames</c> метаданные всех снятых кадров. Пути файлов хранятся относительными (имя файла) —
/// папку серии можно перенести целиком.
/// </summary>
public static class SeriesStore
{
    public const string Extension = ".zisc";
    public const string Format = "zi-scope-series/2";

    /// <summary>Имя проекта без имени серии.</summary>
    private const string DefaultName = "серия";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Имя файла проекта по имени серии: недопустимые символы → «_», пустое имя → «серия».</summary>
    public static string ProjectFileName(SeriesPlan p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var name = (p.Name ?? "").Trim();
        if (name.Length == 0) return DefaultName + Extension;
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()) + Extension;
    }

    /// <summary>Проект серии в папке (если их несколько — самый свежий по времени записи); нет папки или проекта — null.</summary>
    public static string? FindProject(string folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            return new DirectoryInfo(folder).EnumerateFiles("*" + Extension)
                .Where(f => string.Equals(f.Extension, Extension, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>Записать проект атомарно (временный файл и переименование) в <see cref="SeriesSession.ProjectFile"/>.</summary>
    public static void Save(SeriesSession s)
    {
        ArgumentNullException.ThrowIfNull(s);
        Directory.CreateDirectory(s.Folder);
        var done = s.Records.Where(r => r.Value.Status != StepStatus.Pending).ToList();
        var steps = done
            .Select(r => new StepDto(r.Key.Group, r.Key.Sample, r.Key.Magnification, r.Value.Status,
                r.Value.Files.Select(f => ToStored(s.Folder, f)).ToList(), r.Value.When))
            .ToList();
        // Метаданные — по записи на файл снятого шага; имя в записи — то же, что в списке файлов шага.
        var frames = done
            .Where(r => r.Value.Status == StepStatus.Done && r.Value.Frames is not null)
            .SelectMany(r => r.Value.Files.Zip(r.Value.Frames!, (f, m) => m with { File = ToStored(s.Folder, f) }))
            .ToList();
        var dto = new FileDto(Format, s.Plan, ToDto(s.Current), s.Reshooting, s.ExtraSteps.Select(ToDto).ToList(), steps, frames);

        Settings.AtomicFile.WriteAllText(s.ProjectFile, JsonSerializer.Serialize(dto, Json));
    }

    /// <summary>
    /// Открыть проект серии. Папка серии — папка файла; сохраняется проект в этот же файл. Снятые шаги, у которых
    /// нет файлов или пропал хоть один файл, становятся неснятыми. Нет файла — <see cref="FileNotFoundException"/>;
    /// битый, чужой или другой версии формата — <see cref="InvalidDataException"/>.
    /// </summary>
    public static SeriesSession Load(string projectFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFile);
        var path = Path.GetFullPath(projectFile);
        if (!File.Exists(path)) throw new FileNotFoundException("Нет файла проекта серии.", path);
        var folder = Path.GetDirectoryName(path)!;
        FileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Файл проекта серии повреждён: " + path, ex);
        }
        if (dto?.Format is null || !dto.Format.StartsWith("zi-scope-series/", StringComparison.Ordinal))
            throw new InvalidDataException("Файл не является проектом серии ZI-scope: " + path);
        if (dto.Format != Format)
            throw new InvalidDataException($"Версия проекта серии не поддерживается ({dto.Format}): " + path);
        if (dto.Plan is null || dto.Plan.Magnifications is null || dto.Plan.Kinds is null)
            throw new InvalidDataException("Файл проекта серии повреждён: " + path);

        var plan = dto.Plan with { Name = dto.Plan.Name ?? "", FileNameTemplate = dto.Plan.FileNameTemplate ?? SeriesPlan.DefaultTemplate };
        var session = new SeriesSession(plan, folder, path);

        var meta = new Dictionary<string, FrameMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in dto.Frames ?? new())
            if (m?.File is { Length: > 0 } f) meta[FromStored(folder, f)] = m;

        var records = new List<(SeriesStepKey, StepRecord)>();
        var lost = new HashSet<SeriesStepKey>();
        foreach (var st in dto.Steps ?? new())
        {
            var key = new SeriesStepKey(st.Group, st.Sample, st.Magnification);
            var files = (st.Files ?? new()).Select(f => FromStored(folder, f)).ToArray();
            if (st.Status == StepStatus.Done && (files.Length == 0 || files.Any(f => !File.Exists(f))))
            {
                lost.Add(key);
                continue;
            }
            // В записи шага File — имя файла, как его вернул экспорт. Файлы без записи (правленый вручную проект)
            // просто остаются без метаданных.
            var frames = st.Status == StepStatus.Done
                ? files.Where(meta.ContainsKey).Select(f => meta[f] with { File = Path.GetFileName(f) }).ToArray()
                : null;
            records.Add((key, new StepRecord(st.Status, files, st.When, frames is { Length: > 0 } ? frames : null)));
        }
        var extra = (dto.Extra ?? new()).Select(FromDto).Where(k => !lost.Contains(k));
        session.Restore(records, extra, FromDto(dto.Current ?? new KeyDto(1, 1, 0)), dto.Reshooting);
        return session;
    }

    private static string ToStored(string folder, string file)
    {
        var rel = Path.GetRelativePath(folder, file);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? file : rel;
    }

    private static string FromStored(string folder, string file) =>
        Path.IsPathRooted(file) ? file : Path.Combine(folder, file);

    private static KeyDto ToDto(SeriesStepKey k) => new(k.Group, k.Sample, k.Magnification);
    private static SeriesStepKey FromDto(KeyDto k) => new(k.Group, k.Sample, k.Magnification);

    private sealed record KeyDto(int Group, int Sample, int Magnification);

    private sealed record StepDto(int Group, int Sample, int Magnification, StepStatus Status, List<string>? Files, DateTime? When);

    private sealed record FileDto(
        [property: JsonPropertyName("format")] string? Format,
        SeriesPlan? Plan, KeyDto? Current, bool Reshooting, List<KeyDto>? Extra, List<StepDto>? Steps, List<FrameMetadata>? Frames);
}
