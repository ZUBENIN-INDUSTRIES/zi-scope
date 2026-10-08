using LumeneraViewer.Imaging;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Tests;

public sealed class MagnificationSettingsTests
{
    private static readonly ImageAdjustments Warm = new(Brightness: 12, Temperature: 40);
    private static readonly ImageAdjustments Cold = new(Contrast: -8, Temperature: -30, Grayscale: true);

    [Fact]
    public void Defaults_100And200_Current100_NoCalibration()
    {
        var s = new AppSettings();
        Assert.Equal(100, s.CurrentMagnification);
        Assert.Equal(new[] { 100, 200 }, s.Magnifications.Select(m => m.Magnification));
        Assert.All(s.Magnifications, m => Assert.Null(m.MicronsPerPixel));
        Assert.All(s.Magnifications, m => Assert.True(m.Adjustments.IsIdentity));
        Assert.Null(MagnificationSettings.CurrentMicronsPerPixel(s));
    }

    [Fact]
    public void Switch100To200To100_KeepsEachProfileValues()
    {
        var s = new AppSettings();
        // на ×100
        s.Adjustments = Warm; s.ExposureMs = 20; s.Gain = 1.5f; s.ScaleBar.FixedMicrons = 200;
        MagnificationSettings.StoreLive(s);
        MagnificationSettings.LoadLive(s, 200);
        Assert.Equal(200, s.CurrentMagnification);
        Assert.True(s.Adjustments.IsIdentity);           // у ×200 пока нейтральный цвет
        Assert.Null(s.ScaleBar.FixedMicrons);            // и «Авто»

        // на ×200
        s.Adjustments = Cold; s.ExposureMs = 80; s.Gain = 3f; s.ScaleBar.FixedMicrons = 50;
        MagnificationSettings.StoreLive(s);
        MagnificationSettings.LoadLive(s, 100);
        Assert.Equal(100, s.CurrentMagnification);
        Assert.Equal(Warm, s.Adjustments);
        Assert.Equal(20f, s.ExposureMs);
        Assert.Equal(1.5f, s.Gain);
        Assert.Equal(200, s.ScaleBar.FixedMicrons);

        MagnificationSettings.StoreLive(s);
        MagnificationSettings.LoadLive(s, 200);
        Assert.Equal(Cold, s.Adjustments);
        Assert.Equal(80f, s.ExposureMs);
        Assert.Equal(3f, s.Gain);
        Assert.Equal(50, s.ScaleBar.FixedMicrons);
    }

    [Fact]
    public void LoadLive_ProfileWithoutExposure_KeepsCurrentExposure()
    {
        var s = new AppSettings { ExposureMs = 33, Gain = 2 };
        MagnificationSettings.StoreLive(s);
        MagnificationSettings.LoadLive(s, 500);          // новый профиль, экспозиции в нём нет
        Assert.Equal(33f, s.ExposureMs);
        Assert.Equal(2f, s.Gain);
    }

    [Fact]
    public void GetOrCreate_AddsSortedProfile_OnceOnly()
    {
        var s = new AppSettings();
        var p = MagnificationSettings.GetOrCreate(s, 150);
        Assert.Same(p, MagnificationSettings.GetOrCreate(s, 150));
        Assert.Equal(new[] { 100, 150, 200 }, s.Magnifications.Select(m => m.Magnification));
        Assert.True(p.Adjustments.IsIdentity);
        Assert.Null(p.ScaleBarMicrons);
    }

    [Fact]
    public void CurrentMicronsPerPixel_FromCurrentMagnificationProfile()
    {
        var s = new AppSettings();
        MagnificationSettings.GetOrCreate(s, 100).MicronsPerPixel = 0.82;
        MagnificationSettings.GetOrCreate(s, 200).MicronsPerPixel = 0.41;
        Assert.Equal(0.82, MagnificationSettings.CurrentMicronsPerPixel(s));
        MagnificationSettings.LoadLive(s, 200);
        Assert.Equal(0.41, MagnificationSettings.CurrentMicronsPerPixel(s));
        MagnificationSettings.LoadLive(s, 1000);
        Assert.Null(MagnificationSettings.CurrentMicronsPerPixel(s));
    }

    [Fact]
    public void LegacyCalibrations_OnlyObjectivesWithMicronsPerPixel()
    {
        var s = new AppSettings
        {
            Objectives = { new ObjectiveProfile("10x", 0.821), new ObjectiveProfile("40x", 0.2) },
        };
        Assert.Equal(new[] { new ObjectiveProfile("10x", 0.821), new ObjectiveProfile("40x", 0.2) },
            MagnificationSettings.LegacyCalibrations(s));
        Assert.Empty(MagnificationSettings.LegacyCalibrations(new AppSettings()));
    }

    [Fact]
    public void Remove_RefusesCurrentAndLast_RemovesOther()
    {
        var s = new AppSettings();                       // ×100 (текущее), ×200
        Assert.False(MagnificationSettings.Remove(s, 100));   // текущее
        Assert.False(MagnificationSettings.Remove(s, 500));   // нет такого
        Assert.True(MagnificationSettings.Remove(s, 200));
        Assert.Equal(new[] { 100 }, s.Magnifications.Select(m => m.Magnification));

        s.CurrentMagnification = 1000;                   // профиль ×100 — последний, хотя и не текущий
        Assert.False(MagnificationSettings.Remove(s, 100));
        Assert.Single(s.Magnifications);
    }

    [Fact]
    public void CopyFrom_LoadedConfiguration_KeepsItsMagnificationProfiles()
    {
        var target = new AppSettings { Adjustments = Cold, CurrentMagnification = 200 };
        MagnificationSettings.GetOrCreate(target, 200).MicronsPerPixel = 9;

        var loaded = new AppSettings { CurrentMagnification = 500, Adjustments = Warm, ExposureMs = 33 };
        loaded.ScaleBar.FixedMicrons = 50;
        MagnificationSettings.GetOrCreate(loaded, 100).MicronsPerPixel = 0.8;
        // профиля ×500 в файле нет, живые значения — его

        target.CopyFrom(loaded);

        Assert.Equal(500, target.CurrentMagnification);
        Assert.Equal(Warm, target.Adjustments);
        Assert.Equal(new[] { 100, 200, 500 }, target.Magnifications.Select(m => m.Magnification));
        Assert.Equal(0.8, target.Magnifications[0].MicronsPerPixel);
        Assert.Null(target.Magnifications[1].MicronsPerPixel);       // старые настройки не протекли
        var cur = target.Magnifications[2];
        Assert.Equal(Warm, cur.Adjustments);
        Assert.Equal(33f, cur.ExposureMs);
        Assert.Equal(50, cur.ScaleBarMicrons);

        // переключение туда и обратно не теряет живые значения загруженного ×500
        MagnificationSettings.StoreLive(target);
        MagnificationSettings.LoadLive(target, 100);
        MagnificationSettings.StoreLive(target);
        MagnificationSettings.LoadLive(target, 500);
        Assert.Equal(Warm, target.Adjustments);
        Assert.Equal(50, target.ScaleBar.FixedMicrons);
    }
}
