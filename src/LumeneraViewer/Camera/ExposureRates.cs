namespace LumeneraViewer.Camera;

/// <summary>
/// Частота кадров ограничивает выдержку LuCam: на каждой частоте драйвер сообщает свой максимум экспозиции.
/// Для длинной выдержки нужна частота пониже, для короткой выгоднее самая быстрая (живее превью).
/// </summary>
public static class ExposureRates
{
    /// <summary>Самая низкая выдержка, которую показываем: драйвер сообщает минимум 0, а логарифмической шкале нужен &gt; 0.</summary>
    public const float MinExposureMs = 0.01f;

    /// <summary>Самая быстрая частота, на которой допустима выдержка <paramref name="exposureMs"/>; если нет такой — самая медленная.</summary>
    public static float Choose(IReadOnlyList<(float Rate, float MaxExposureMs)> table, float exposureMs)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count == 0) throw new ArgumentException("Пустая таблица частот", nameof(table));
        float? best = null;
        foreach (var (rate, max) in table)
            if (max >= exposureMs * 0.999f && (best is null || rate > best)) best = rate;
        return best ?? table.MinBy(t => t.Rate).Rate;
    }

    /// <summary>Наибольшая выдержка по всем частотам.</summary>
    public static float MaxExposure(IReadOnlyList<(float Rate, float MaxExposureMs)> table)
        => table.Count == 0 ? 0 : table.Max(t => t.MaxExposureMs);
}
