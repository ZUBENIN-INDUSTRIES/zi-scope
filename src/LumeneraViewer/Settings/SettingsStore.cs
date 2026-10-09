using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LumeneraViewer.Settings;

/// <summary>Хранилище настроек в JSON. Битый файл переименовывается в .bak, возвращаются значения по умолчанию.</summary>
public sealed class SettingsStore : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // кириллица читаемая
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _lock = new();
    private readonly Timer _timer;
    private readonly object _writeLock = new();
    private string? _pendingJson;
    private bool _disposed;

    /// <summary>Папка данных программы: %AppData%\ZI-scope.</summary>
    public static string AppDataFolder { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZI-scope");

    /// <param name="path">Файл настроек; null — %AppData%\ZI-scope\settings.json.</param>
    /// <param name="legacyPath">Старый файл, который копируется, если <paramref name="path"/> нет;
    /// при path = null — %AppData%\LumeneraViewer\settings.json.</param>
    public SettingsStore(string? path = null, string? legacyPath = null)
    {
        Path = path ?? System.IO.Path.Combine(AppDataFolder, "settings.json");
        LegacyPath = legacyPath ?? (path is null
            ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumeneraViewer", "settings.json")
            : null);
        _timer = new Timer(_ => FlushSafe());   // исключение из потока пула уронило бы процесс
    }

    public string Path { get; }

    /// <summary>Файл настроек LumeneraViewer для однократной миграции; null — без миграции.</summary>
    public string? LegacyPath { get; }

    public AppSettings Load()
    {
        var migrated = MigrateLegacyFile();
        if (!File.Exists(Path)) return new AppSettings();
        try
        {
            return Parse(File.ReadAllText(Path)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            try { File.Move(Path, Path + ".bak", overwrite: true); } catch (IOException) { }
            var defaults = new AppSettings();
            // Битая копия старого файла: записать умолчания, иначе следующий запуск снова скопирует
            // старый файл и затрёт .bak
            if (migrated)
            {
                try { Save(defaults); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            return defaults;
        }
    }

    /// <summary>Нового файла нет, а старый (LumeneraViewer) есть — скопировать; старый не трогается. true — скопирован.</summary>
    private bool MigrateLegacyFile()
    {
        if (LegacyPath is null || File.Exists(Path) || !File.Exists(LegacyPath)) return false;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.Copy(LegacyPath, Path, overwrite: false);
            return true;
        }
        catch (IOException) { return false; /* не удалось — начнём с умолчаний, старый файл цел */ }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static JsonSerializerOptions JsonOptions => Options;

    /// <summary>JSON настроек → AppSettings с починкой null и однократными миграциями; null — в JSON не объект.</summary>
    internal static AppSettings? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
        var s = doc.RootElement.Deserialize<AppSettings>(Options);
        if (s is null) return null;
        var hasMagnifications = doc.RootElement.TryGetProperty(nameof(AppSettings.Magnifications), out _);
        return Migrate(Repair(s), hasMagnifications);
    }

    public void Save(AppSettings s) => WriteJson(JsonSerializer.Serialize(s, Options));

    /// <summary>Ошибка отложенного сохранения (поток пула); исключение наружу не выходит.</summary>
    public event Action<Exception>? SaveFailed;

    /// <summary>Сохранить через 1 с после последнего вызова. Сериализуется снимок на момент вызова.</summary>
    public void ScheduleSave(AppSettings s)
    {
        string json;
        try { json = JsonSerializer.Serialize(s, Options); }
        catch (Exception ex) { SaveFailed?.Invoke(ex); return; }
        lock (_lock)
        {
            if (_disposed) return;
            _pendingJson = json;
            _timer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Немедленно записать отложенное сохранение, если оно есть. Ошибки записи бросаются.</summary>
    public void Flush()
    {
        string? json;
        lock (_lock)
        {
            json = _pendingJson;
            _pendingJson = null;
            if (!_disposed) _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        if (json is not null) WriteJson(json);
    }

    /// <summary>Flush (без исключений, ошибка → SaveFailed) и остановка таймера.</summary>
    public void Dispose()
    {
        FlushSafe();
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
        }
    }

    private void FlushSafe()
    {
        try { Flush(); }
        catch (Exception ex) { SaveFailed?.Invoke(ex); }
    }

    private void WriteJson(string json)
    {
        lock (_writeLock)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            AtomicFile.WriteAllText(Path, json);   // tmp → Flush(true) → замена, tmp убирается при сбое
        }
    }

    // null из JSON («"Camera": null») заменяется значениями по умолчанию
    internal static AppSettings Repair(AppSettings s)
    {
        s.Camera ??= new();
        s.Adjustments ??= Imaging.ImageAdjustments.Default;
        s.Presets ??= new();
        s.Objectives ??= new();
        if (s.Objectives.Count == 0) s.Objectives.Add(new ObjectiveProfile("Объектив 1", null));
        s.CurrentObjective ??= s.Objectives[0].Name;
        s.ScaleBar ??= new();
        s.Guides ??= new();
        s.Export ??= new();
        s.Timelapse ??= new();
        s.Window ??= new();
        s.Extensions ??= new();

        s.ColorProfiles ??= new();
        s.ColorProfiles.RemoveAll(p => p is null);
        foreach (var p in s.ColorProfiles)
        {
            p.Name ??= "";
            p.Adjustments ??= Imaging.ImageAdjustments.Default;
        }
        s.SeriesDefaults ??= new();
        var d = s.SeriesDefaults;
        if (d.Magnifications is null || d.Magnifications.Count == 0) d.Magnifications = new() { 100, 200 };
        if (d.Kinds is null || d.Kinds.Count == 0) d.Kinds = Series.SeriesPlan.DefaultKinds.ToList();
        d.FileNameTemplate ??= Series.SeriesPlan.DefaultTemplate;

        s.Magnifications ??= AppSettings.DefaultMagnifications();
        s.Magnifications.RemoveAll(m => m is null || m.Magnification <= 0);
        if (s.Magnifications.Count == 0) s.Magnifications = AppSettings.DefaultMagnifications();
        foreach (var m in s.Magnifications) m.Adjustments ??= Imaging.ImageAdjustments.Default;
        s.Magnifications = s.Magnifications.GroupBy(m => m.Magnification).Select(g => g.First())
            .OrderBy(m => m.Magnification).ToList();
        if (s.CurrentMagnification <= 0) s.CurrentMagnification = s.Magnifications[0].Magnification;
        MagnificationSettings.GetOrCreate(s, s.CurrentMagnification);
        return s;
    }

    private static string UniqueProfileName(List<ColorProfile> existing, string name)
    {
        bool Taken(string n) => existing.Any(c => string.Equals(c.Name, n, StringComparison.CurrentCultureIgnoreCase));
        if (!Taken(name)) return name;
        for (var i = 2; ; i++)
        {
            var candidate = $"{name} ({i})";
            if (!Taken(candidate)) return candidate;
        }
    }

    private static AppSettings Migrate(AppSettings s, bool hasMagnifications)
    {
        // Настройки до ZI-scope: живой цвет и экспозиция становятся профилем текущего увеличения.
        // Длина плашки и калибровка объектива автоматически не переносятся (соответствие знает только пользователь).
        if (!hasMagnifications)
        {
            var p = MagnificationSettings.GetOrCreate(s, s.CurrentMagnification);
            p.Adjustments = s.Adjustments;
            p.ExposureMs = s.ExposureMs;
            p.Gain = s.Gain;
            s.ScaleBar.FixedMicrons = p.ScaleBarMicrons;
        }

        // Старые пресеты → профили цвета, один раз (Presets очищается). Совпавшее имя (без учёта регистра)
        // получает суффикс « (2)», « (3)» … — пресет не теряется.
        if (s.Presets.Count > 0)
        {
            foreach (var pr in s.Presets)
            {
                if (pr is null) continue;
                s.ColorProfiles.Add(new ColorProfile
                {
                    Name = UniqueProfileName(s.ColorProfiles, pr.Name ?? ""),
                    Adjustments = pr.Adjustments ?? Imaging.ImageAdjustments.Default,
                    IncludesExposure = pr.ExposureMs is not null || pr.Gain is not null,
                    ExposureMs = pr.ExposureMs,
                    Gain = pr.Gain,
                });
            }
            s.Presets.Clear();
        }
        return s;
    }
}
