using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LumeneraViewer.Settings;

/// <summary>
/// Профили цвета (<c>.zcolor</c>, один <see cref="ColorProfile"/>) и конфигурации (<c>.zscope</c>, весь
/// <see cref="AppSettings"/>) в файлах. Первое поле — <c>"format"</c>; файл из более новой версии того же
/// вида (<c>zi-scope-color/2</c> …) читается, неизвестные поля пропускаются.
/// </summary>
public static class ProfileFiles
{
    public const string ColorFormat = "zi-scope-color/1";
    public const string ConfigFormat = "zi-scope-config/1";
    public const string ColorExtension = ".zcolor";
    public const string ConfigExtension = ".zscope";
    public const string NotAProfileMessage = "Файл не является профилем ZI-scope";

    private const string FormatField = "format";

    /// <summary>Прочитать профиль цвета. Битый или чужой файл — <see cref="InvalidDataException"/>.</summary>
    public static ColorProfile ReadColor(string path)
    {
        var root = ReadRoot(path, ColorFormat);
        try
        {
            var p = root.Deserialize<ColorProfile>(SettingsStore.JsonOptions) ?? throw new InvalidDataException(NotAProfileMessage);
            p.Name ??= "";
            p.Adjustments ??= Imaging.ImageAdjustments.Default;
            return p;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException(NotAProfileMessage, ex);
        }
    }

    public static void WriteColor(string path, ColorProfile p)
    {
        ArgumentNullException.ThrowIfNull(p);
        Write(path, ColorFormat, JsonSerializer.SerializeToNode(p, SettingsStore.JsonOptions)!.AsObject());
    }

    /// <summary>Прочитать конфигурацию (с теми же починками и миграциями, что и settings.json).</summary>
    public static AppSettings ReadConfig(string path)
    {
        var root = ReadRoot(path, ConfigFormat);
        try
        {
            return SettingsStore.Parse(root.GetRawText()) ?? throw new InvalidDataException(NotAProfileMessage);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException(NotAProfileMessage, ex);
        }
    }

    /// <summary>Записать конфигурацию. Список профилей конфигурации — это файлы, внутрь не попадает.</summary>
    public static void WriteConfig(string path, AppSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        Write(path, ConfigFormat, JsonSerializer.SerializeToNode(s, SettingsStore.JsonOptions)!.AsObject());
    }

    /// <summary>%AppData%\ZI-scope\profiles — сохранённые профили конфигурации.</summary>
    public static string ConfigProfilesFolder { get; } = Path.Combine(SettingsStore.AppDataFolder, "profiles");

    /// <summary>
    /// Папка профилей конфигурации для файла настроек: <c>profiles</c> рядом с ним. Для обычного
    /// %AppData%\ZI-scope\settings.json это <see cref="ConfigProfilesFolder"/>; при <c>--settings</c> — рядом с указанным файлом.
    /// </summary>
    public static string ConfigProfilesFolderFor(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "profiles");
    }

    /// <summary>Файлы <c>.zscope</c> в <see cref="ConfigProfilesFolder"/>, по имени.</summary>
    public static IReadOnlyList<string> ListConfigProfiles() => ListConfigProfiles(ConfigProfilesFolder);

    internal static IReadOnlyList<string> ListConfigProfiles(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        return Directory.EnumerateFiles(folder, "*" + ConfigExtension)
            .Where(f => string.Equals(Path.GetExtension(f), ConfigExtension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static JsonElement ReadRoot(string path, string expectedFormat)
    {
        var text = File.ReadAllText(path);

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(FormatField, out var f) || f.ValueKind != JsonValueKind.String
                || !SameKind(f.GetString()!, expectedFormat))
                throw new InvalidDataException(NotAProfileMessage);
            return root.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(NotAProfileMessage, ex);
        }
    }

    // «zi-scope-color/1» и «zi-scope-color/2» — один вид профиля, разные версии
    private static bool SameKind(string actual, string expected)
    {
        var kind = expected[..(expected.IndexOf('/') + 1)];
        return actual.StartsWith(kind, StringComparison.Ordinal) && actual.Length > kind.Length;
    }

    private static void Write(string path, string format, JsonObject body)
    {
        var o = new JsonObject { [FormatField] = format };
        foreach (var (k, v) in body.ToList())
        {
            body.Remove(k);
            o[k] = v;
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        AtomicFile.WriteAllText(path, o.ToJsonString(SettingsStore.JsonOptions));
    }
}
