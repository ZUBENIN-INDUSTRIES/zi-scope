using LumeneraViewer.Imaging;

namespace LumeneraViewer.Settings;

/// <summary>
/// Профили увеличений. Живые значения (<see cref="AppSettings.Adjustments"/>, ExposureMs, Gain,
/// ScaleBar.FixedMicrons) всегда относятся к <see cref="AppSettings.CurrentMagnification"/>:
/// при переключении они сохраняются в профиль старого увеличения и загружаются из нового.
/// </summary>
public static class MagnificationSettings
{
    /// <summary>Профиль увеличения; если его нет — создаётся нейтральный (список остаётся упорядоченным).</summary>
    public static MagnificationProfile GetOrCreate(AppSettings s, int mag)
    {
        s.Magnifications ??= new();
        var p = Find(s, mag);
        if (p is not null) return p;

        p = new MagnificationProfile { Magnification = mag };
        var at = s.Magnifications.FindIndex(m => m.Magnification > mag);
        if (at < 0) s.Magnifications.Add(p);
        else s.Magnifications.Insert(at, p);
        return p;
    }

    /// <summary>Живые цвет, экспозиция, усиление и длина плашки → профиль текущего увеличения.</summary>
    public static void StoreLive(AppSettings s)
    {
        var p = GetOrCreate(s, s.CurrentMagnification);
        p.Adjustments = s.Adjustments ?? ImageAdjustments.Default;
        p.ExposureMs = s.ExposureMs;
        p.Gain = s.Gain;
        p.ScaleBarMicrons = s.ScaleBar?.FixedMicrons;
    }

    /// <summary>
    /// Профиль <paramref name="mag"/> → живые значения, <see cref="AppSettings.CurrentMagnification"/> = mag.
    /// Живые значения прежнего увеличения сначала сохранить через <see cref="StoreLive"/>.
    /// Если в профиле нет экспозиции или усиления (null), живые остаются как есть — камера их не меняла.
    /// </summary>
    public static void LoadLive(AppSettings s, int mag)
    {
        var p = GetOrCreate(s, mag);
        s.CurrentMagnification = mag;
        s.Adjustments = p.Adjustments ?? ImageAdjustments.Default;
        if (p.ExposureMs is not null) s.ExposureMs = p.ExposureMs;
        if (p.Gain is not null) s.Gain = p.Gain;
        s.ScaleBar ??= new();
        s.ScaleBar.FixedMicrons = p.ScaleBarMicrons;
    }

    /// <summary>Мкм/px профиля текущего увеличения; null — не откалибровано.</summary>
    public static double? CurrentMicronsPerPixel(AppSettings s) => Find(s, s.CurrentMagnification)?.MicronsPerPixel;

    /// <summary>Старые профили объективов с калибровкой — источник для ручного переноса в увеличения.</summary>
    public static IReadOnlyList<ObjectiveProfile> LegacyCalibrations(AppSettings s) =>
        (s.Objectives ?? new()).Where(o => o.MicronsPerPixel is > 0).ToList();

    /// <summary>Допустимое увеличение: целое 1–10000.</summary>
    public const int MinMagnification = 1, MaxMagnification = 10000;

    public static bool IsValid(int mag) => mag is >= MinMagnification and <= MaxMagnification;

    /// <summary>Удалить профиль увеличения. Текущее и последнее не удаляются; false — ничего не удалено.</summary>
    public static bool Remove(AppSettings s, int mag)
    {
        if (mag == s.CurrentMagnification || s.Magnifications is not { Count: > 1 } list) return false;
        return list.RemoveAll(m => m.Magnification == mag) > 0;
    }

    private static MagnificationProfile? Find(AppSettings s, int mag) =>
        s.Magnifications?.FirstOrDefault(m => m.Magnification == mag);
}
