using System.IO;
using LumeneraViewer.Export;

namespace LumeneraViewer.Series;

public enum StepStatus { Pending, Done, Skipped }

/// <summary>
/// Состояние шага: статус, записанные файлы (полные пути), время съёмки и метаданные кадров —
/// по одной записи на файл из <see cref="Files"/> в том же порядке (<see cref="FrameMetadata.File"/> — имя файла);
/// <see cref="SkippedKinds"/> — номера дополнительных кадров, которые лаборант пропустил.
/// </summary>
public sealed record StepRecord(StepStatus Status, IReadOnlyList<string> Files, DateTime? When, IReadOnlyList<FrameMetadata>? Frames = null,
    IReadOnlyList<int>? SkippedKinds = null)
{
    public static StepRecord Pending { get; } = new(StepStatus.Pending, Array.Empty<string>(), null);
}

/// <summary>
/// Идущая серия: статусы шагов, текущий шаг, пересъёмка, шаги вне плана. Не потокобезопасна —
/// вызывать из одного (UI) потока.
/// </summary>
public sealed class SeriesSession
{
    private readonly IReadOnlyList<SeriesStepKey> _steps;
    private readonly Dictionary<SeriesStepKey, int> _index = new();
    private readonly Dictionary<SeriesStepKey, StepRecord> _records = new();
    private readonly List<SeriesStepKey> _extra = new();
    private bool _returnToFirstPending;

    /// <param name="projectFile">Полный путь файла проекта <c>.zisc</c>; null — <c>&lt;папка&gt;\&lt;имя серии&gt;.zisc</c>.</param>
    public SeriesSession(SeriesPlan plan, string folder, string? projectFile = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plan = plan;
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        ProjectFile = projectFile ?? Path.Combine(folder, SeriesStore.ProjectFileName(plan));
        _steps = plan.EnumerateSteps();
        for (var i = 0; i < _steps.Count; i++)
        {
            _index.TryAdd(_steps[i], i);
            _records[_steps[i]] = StepRecord.Pending;
        }
        Current = _steps.Count > 0 ? _steps[0] : default;
    }

    public SeriesPlan Plan { get; }
    public string Folder { get; }

    /// <summary>Файл проекта серии (<c>.zisc</c>), в который пишется состояние.</summary>
    public string ProjectFile { get; }

    /// <summary>Шаг, который снимет следующее «Снять шаг» (может быть вне плана).</summary>
    public SeriesStepKey Current { get; private set; }

    /// <summary>Текущий шаг переснимается: его файлы перезаписываются без вопросов.</summary>
    public bool Reshooting { get; private set; }

    /// <summary>Записи всех шагов плана и шагов вне плана.</summary>
    public IReadOnlyDictionary<SeriesStepKey, StepRecord> Records => _records;

    /// <summary>Снятые шаги с номерами, которых нет в плане, в порядке съёмки.</summary>
    public IReadOnlyList<SeriesStepKey> ExtraSteps => _extra;

    /// <summary>Снятых шагов плана (шаги вне плана не считаются).</summary>
    public int DoneCount => _steps.Count(k => _records[k].Status == StepStatus.Done);

    /// <summary>Неснятых шагов плана не осталось (пропущенные считаются пройденными).</summary>
    public bool IsComplete => NextPending() is null;

    public bool IsInPlan(SeriesStepKey k) => _index.ContainsKey(k);

    public StepRecord RecordOf(SeriesStepKey k) => _records.TryGetValue(k, out var r) ? r : StepRecord.Pending;

    /// <summary>
    /// Первый неснятый шаг плана после <paramref name="after"/> (по кругу) или с начала, если after = null
    /// или вне плана. null — неснятых нет.
    /// </summary>
    public SeriesStepKey? NextPending(SeriesStepKey? after = null)
    {
        var n = _steps.Count;
        var start = after is { } a && _index.TryGetValue(a, out var i) ? i + 1 : 0;
        for (var j = 0; j < n; j++)
        {
            var k = _steps[(start + j) % n];
            if (_records[k].Status == StepStatus.Pending) return k;
        }
        return null;
    }

    /// <summary>
    /// Шаг снят. Шаг вне плана попадает в <see cref="ExtraSteps"/>. Если снят текущий — переход
    /// к следующему неснятому (после пересъёмки или шага вне плана — к первому неснятому).
    /// </summary>
    /// <param name="frames">Метаданные кадров — по одной на файл, в том же порядке; заменяют прежние записи шага.</param>
    /// <param name="skippedKinds">Пропущенные дополнительные кадры шага.</param>
    /// <param name="complete">false — у шага остались дополнительные кадры: он остаётся текущим
    /// (пересъёмка снимается, но после последнего кадра — возврат к первому неснятому, как после пересъёмки).</param>
    public void MarkDone(SeriesStepKey k, IReadOnlyList<string> files, IReadOnlyList<FrameMetadata>? frames = null,
        IReadOnlyList<int>? skippedKinds = null, bool complete = true)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!IsInPlan(k) && !_extra.Contains(k)) _extra.Add(k);
        _records[k] = new StepRecord(StepStatus.Done, files.ToArray(), DateTime.Now, frames?.ToArray(),
            skippedKinds is { Count: > 0 } ? skippedKinds.Distinct().Order().ToArray() : null);
        if (k != Current) return;
        if (!complete)
        {
            if (Reshooting) _returnToFirstPending = true;
            Reshooting = false;
            return;
        }
        Current = (Reshooting || _returnToFirstPending ? NextPending() : NextPending(k)) ?? k;
        Reshooting = false;
        _returnToFirstPending = false;
    }

    /// <summary>
    /// Пропустить шаг; если он текущий — перейти к следующему неснятому. Шаг вне плана не записывается
    /// и в <see cref="ExtraSteps"/> не попадает; уже снятый шаг вне плана не меняется.
    /// </summary>
    public void MarkSkipped(SeriesStepKey k)
    {
        if (IsInPlan(k)) _records[k] = new StepRecord(StepStatus.Skipped, Array.Empty<string>(), null);
        if (k == Current)
        {
            Current = (IsInPlan(k) ? NextPending(k) : NextPending()) ?? k;
            Reshooting = false;
            _returnToFirstPending = false;
        }
    }

    /// <summary>Шаг снова «не снят» (файлы удаляет вызывающий). Шаг вне плана убирается совсем.</summary>
    public void Reset(SeriesStepKey k)
    {
        if (IsInPlan(k)) _records[k] = StepRecord.Pending;
        else
        {
            _records.Remove(k);
            _extra.Remove(k);
        }
    }

    /// <summary>Сделать шаг текущим (любой номер, в том числе вне плана); пересъёмка снимается.</summary>
    public void SetCurrent(SeriesStepKey k)
    {
        Current = k;
        Reshooting = false;
        _returnToFirstPending = false;
    }

    /// <summary>Переснять шаг: он становится текущим, после съёмки — возврат к первому неснятому.</summary>
    public void BeginReshoot(SeriesStepKey k)
    {
        Current = k;
        Reshooting = true;
        _returnToFirstPending = false;
    }

    /// <summary>Восстановление из файла: записи, шаги вне плана, текущий шаг.</summary>
    internal void Restore(IEnumerable<(SeriesStepKey Key, StepRecord Record)> records, IEnumerable<SeriesStepKey> extra,
        SeriesStepKey current, bool reshooting)
    {
        foreach (var (k, r) in records)
            if (IsInPlan(k)) _records[k] = r;
        foreach (var k in extra)
        {
            if (IsInPlan(k) || _extra.Contains(k)) continue;
            _extra.Add(k);
        }
        foreach (var (k, r) in records)
            if (_extra.Contains(k)) _records[k] = r;
        Current = current;
        Reshooting = reshooting;
    }
}
