using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LumeneraViewer.Series;

/// <summary>
/// Состояние серии в файле <c>series.zscope-series.json</c> в папке серии. Пути файлов внутри папки
/// хранятся относительными — папку серии можно перенести целиком.
/// </summary>
public static class SeriesStore
{
    public const string FileName = "series.zscope-series.json";
    public const string Format = "zi-scope-series/1";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Записать состояние атомарно (временный файл и переименование).</summary>
    public static void Save(SeriesSession s)
    {
        ArgumentNullException.ThrowIfNull(s);
        Directory.CreateDirectory(s.Folder);
        var steps = s.Records
            .Where(r => r.Value.Status != StepStatus.Pending)
            .Select(r => new StepDto(r.Key.Group, r.Key.Sample, r.Key.Magnification, r.Value.Status,
                r.Value.Files.Select(f => ToStored(s.Folder, f)).ToList(), r.Value.When))
            .ToList();
        var dto = new FileDto(Format, s.Plan, ToDto(s.Current), s.Reshooting, s.ExtraSteps.Select(ToDto).ToList(), steps);

        Settings.AtomicFile.WriteAllText(Path.Combine(s.Folder, FileName), JsonSerializer.Serialize(dto, Json));
    }

    /// <summary>
    /// Загрузить серию из папки. Снятые шаги, у которых нет файлов или пропал хоть один файл, становятся
    /// неснятыми. Нет файла — <see cref="FileNotFoundException"/>; битый — <see cref="InvalidDataException"/>.
    /// </summary>
    public static SeriesSession Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        if (!File.Exists(path)) throw new FileNotFoundException("В папке нет файла серии.", path);
        FileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Файл серии повреждён: " + path, ex);
        }
        if (dto?.Format is null || !dto.Format.StartsWith("zi-scope-series/", StringComparison.Ordinal))
            throw new InvalidDataException("Файл не является файлом серии ZI-scope: " + path);
        if (dto.Plan is null || dto.Plan.Magnifications is null || dto.Plan.Kinds is null)
            throw new InvalidDataException("Файл серии повреждён: " + path);

        var plan = dto.Plan with { Name = dto.Plan.Name ?? "", FileNameTemplate = dto.Plan.FileNameTemplate ?? SeriesPlan.DefaultTemplate };
        var session = new SeriesSession(plan, folder);

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
            records.Add((key, new StepRecord(st.Status, files, st.When)));
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
        SeriesPlan? Plan, KeyDto? Current, bool Reshooting, List<KeyDto>? Extra, List<StepDto>? Steps);
}
