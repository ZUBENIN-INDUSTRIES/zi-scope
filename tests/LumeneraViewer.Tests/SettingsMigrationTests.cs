using System.IO;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class SettingsMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "zs-mig-" + Guid.NewGuid().ToString("N"));
    private string NewPath => Path.Combine(_dir, "ZI-scope", "settings.json");
    private string OldPath => Path.Combine(_dir, "LumeneraViewer", "settings.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // Файл настроек версии LumeneraViewer: без новых полей, с пресетами и калибровкой объектива
    private const string LegacyJson = """
        {
          "Adjustments": { "Brightness": 15, "Contrast": 0, "Gamma": 1, "Saturation": 100, "Temperature": 20, "Tint": 0, "Grayscale": false, "Negative": false },
          "ExposureMs": 25.5, "Gain": 1.75,
          "Presets": [
            { "Name": "Окраска", "Adjustments": { "Contrast": 15 }, "ExposureMs": 12, "Gain": null },
            { "Name": "Просто цвет", "Adjustments": { "Saturation": 130 }, "ExposureMs": null, "Gain": null }
          ],
          "Objectives": [ { "Name": "10x", "MicronsPerPixel": 0.821 } ],
          "CurrentObjective": "10x",
          "ScaleBar": { "FixedMicrons": 200 },
          "Export": { "Folder": "D:\\Мои снимки", "Format": "Jpeg" }
        }
        """;

    private void WriteOld(string json = LegacyJson)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OldPath)!);
        File.WriteAllText(OldPath, json);
    }

    [Fact]
    public void NoNewFile_OldCopied_OldUntouched()
    {
        WriteOld();
        var before = File.ReadAllBytes(OldPath);
        var s = new SettingsStore(NewPath, OldPath).Load();

        Assert.True(File.Exists(NewPath));
        Assert.Equal(before, File.ReadAllBytes(OldPath));
        Assert.Equal(15, s.Adjustments.Brightness);
        Assert.Equal(@"D:\Мои снимки", s.Export.Folder);     // папка пользователя не меняется
        Assert.Equal(25.5f, s.ExposureMs);
    }

    [Fact]
    public void NewFileExists_OldIgnored()
    {
        WriteOld();
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);
        File.WriteAllText(NewPath, """{ "CurrentMagnification": 500 }""");
        var s = new SettingsStore(NewPath, OldPath).Load();
        Assert.Equal(500, s.CurrentMagnification);
        Assert.Equal(0, s.Adjustments.Brightness);
    }

    [Fact]
    public void NeitherFile_DefaultsWithZiScopeCaptureFolder()
    {
        var s = new SettingsStore(NewPath, OldPath).Load();
        Assert.False(File.Exists(NewPath));
        Assert.Equal("ZI-scope", Path.GetFileName(s.Export.Folder));
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Path.GetDirectoryName(s.Export.Folder));
    }

    [Fact]
    public void LegacyJson_LoadsWithNewDefaults()
    {
        WriteOld();
        var s = new SettingsStore(NewPath, OldPath).Load();
        Assert.Equal(100, s.CurrentMagnification);
        Assert.Equal(new[] { 100, 200 }, s.Magnifications.Select(m => m.Magnification));
        Assert.All(s.Magnifications, m => Assert.Null(m.MicronsPerPixel));   // калибровка автоматически не переносится
        Assert.All(s.Magnifications, m => Assert.Null(m.ScaleBarMicrons));   // и длина плашки тоже
        Assert.Null(s.ScaleBar.FixedMicrons);                                  // живое значение = профиль ×100
        Assert.Equal(16, s.SeriesDefaults.Groups);
        Assert.Equal(4, s.SeriesDefaults.SamplesPerGroup);
        Assert.Equal(new[] { 100, 200 }, s.SeriesDefaults.Magnifications);
        Assert.Equal(2, s.SeriesDefaults.Kinds.Count);
        Assert.Null(s.LastSeriesFolder);
        Assert.False(s.LegacyCalibrationDismissed);
        Assert.Single(MagnificationSettings.LegacyCalibrations(s));          // старая калибровка доступна для ручного переноса
    }

    [Fact]
    public void LegacyJson_LiveColorBecomesCurrentMagnificationProfile()
    {
        WriteOld();
        var s = new SettingsStore(NewPath, OldPath).Load();
        var p100 = s.Magnifications.Single(m => m.Magnification == 100);
        Assert.Equal(15, p100.Adjustments.Brightness);
        Assert.Equal(20, p100.Adjustments.Temperature);
        Assert.Equal(25.5f, p100.ExposureMs);
        Assert.Equal(1.75f, p100.Gain);
    }

    [Fact]
    public void Presets_MovedToColorProfilesOnce()
    {
        WriteOld();
        var store = new SettingsStore(NewPath, OldPath);
        var s = store.Load();
        Assert.Empty(s.Presets);
        Assert.Equal(new[] { "Окраска", "Просто цвет" }, s.ColorProfiles.Select(p => p.Name));
        var a = s.ColorProfiles[0];
        Assert.Equal(15, a.Adjustments.Contrast);
        Assert.True(a.IncludesExposure);
        Assert.Equal(12f, a.ExposureMs);
        Assert.False(s.ColorProfiles[1].IncludesExposure);

        store.Save(s);
        var again = new SettingsStore(NewPath, OldPath).Load();
        Assert.Equal(2, again.ColorProfiles.Count);
        Assert.Empty(again.Presets);
    }

    [Fact]
    public void PresetWithExistingName_KeptWithSuffix()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);
        File.WriteAllText(NewPath, """
            { "Presets": [ { "Name": "окраска", "Adjustments": { "Contrast": 40 } },
                           { "Name": "Окраска", "Adjustments": { "Contrast": 50 } } ],
              "ColorProfiles": [ { "Name": "Окраска", "Adjustments": { "Contrast": 15 } } ] }
            """);
        var s = new SettingsStore(NewPath).Load();
        Assert.Equal(new[] { "Окраска", "окраска (2)", "Окраска (3)" }, s.ColorProfiles.Select(p => p.Name));
        Assert.Equal(new double[] { 15, 40, 50 }, s.ColorProfiles.Select(p => p.Adjustments.Contrast));
        Assert.Empty(s.Presets);
    }

    [Fact]
    public void NullNewFields_Repaired()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);
        File.WriteAllText(NewPath, """
            { "CurrentMagnification": 0, "Magnifications": null, "ColorProfiles": null, "SeriesDefaults": null }
            """);
        var s = new SettingsStore(NewPath).Load();
        Assert.Equal(100, s.CurrentMagnification);
        Assert.Equal(new[] { 100, 200 }, s.Magnifications.Select(m => m.Magnification));
        Assert.Empty(s.ColorProfiles);
        Assert.Equal(16, s.SeriesDefaults.Groups);
    }

    [Fact]
    public void BrokenLegacyFile_MigratedOnlyOnce_BakNotOverwritten()
    {
        WriteOld("{ \"Camera\": { oops");
        var first = new SettingsStore(NewPath, OldPath).Load();
        Assert.Equal(1, first.Camera.CameraIndex);
        Assert.Equal("{ \"Camera\": { oops", File.ReadAllText(NewPath + ".bak"));
        Assert.True(File.Exists(NewPath));                 // умолчания записаны — повторной миграции не будет
        Assert.Equal("{ \"Camera\": { oops", File.ReadAllText(OldPath));

        File.WriteAllText(NewPath + ".bak", "метка");
        var second = new SettingsStore(NewPath, OldPath).Load();
        Assert.Equal(1, second.Camera.CameraIndex);
        Assert.Equal("метка", File.ReadAllText(NewPath + ".bak"));
    }
}
