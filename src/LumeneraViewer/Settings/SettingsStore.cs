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

    public SettingsStore(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumeneraViewer", "settings.json");
        _timer = new Timer(_ => FlushSafe());   // исключение из потока пула уронило бы процесс
    }

    public string Path { get; }

    public AppSettings Load()
    {
        if (!File.Exists(Path)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(Path);
            var s = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return s is null ? new AppSettings() : Repair(s);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            try { File.Move(Path, Path + ".bak", overwrite: true); } catch (IOException) { }
            return new AppSettings();
        }
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
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, Path, overwrite: true);
        }
    }

    // null из JSON («"Camera": null») заменяется значениями по умолчанию
    private static AppSettings Repair(AppSettings s)
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
        return s;
    }
}
