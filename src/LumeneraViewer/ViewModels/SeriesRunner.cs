using System.IO;
using LumeneraViewer.Export;
using LumeneraViewer.Series;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Всё, что прогону серии нужно от программы: увеличение хоста, калибровка, файлы, вопрос о перезаписи,
/// запись состояния. В программе — <see cref="SeriesPanelViewModel"/>, в тестах — подмена.
/// </summary>
public interface ISeriesEnvironment
{
    /// <summary>Текущее увеличение программы; установка применяет профиль увеличения (калибровка, цвет).</summary>
    int CurrentMagnification { get; set; }
    /// <summary>У увеличения есть калибровка мкм/px.</summary>
    bool IsCalibrated(int magnification);
    /// <summary>Расширение файлов серии (с точкой) — из настроек экспорта.</summary>
    string FileExtension { get; }
    bool FileExists(string path);
    /// <summary>Удалить файл; отсутствующий файл — не ошибка.</summary>
    void DeleteFile(string path);
    /// <summary>Переименовать файл, заменив существующий <paramref name="to"/>.</summary>
    void MoveFile(string from, string to);
    /// <summary>Удалить папку, если она есть и пуста.</summary>
    void DeleteEmptyFolder(string path);
    /// <summary>Файлы шага уже есть, а шаг не переснимается: true — перезаписать.</summary>
    Task<bool> ConfirmOverwriteAsync(IReadOnlyList<string> existing);
    /// <summary>Записать проект серии (.zisc).</summary>
    void Persist(SeriesSession session);
}

/// <summary>Чем кончилось «Снять шаг».</summary>
public enum StepCaptureResult
{
    /// <summary>Файлы записаны, шаг отмечен снятым, текущий — следующий неснятый.</summary>
    Captured,
    /// <summary>Увеличение программы не совпадало с увеличением шага — переключено, ничего не снято.</summary>
    MagnificationSwitched,
    /// <summary>Есть вид с масштабом, а увеличение шага не откалибровано — ничего не снято.</summary>
    NeedsCalibration,
    /// <summary>Отказ перезаписать существующие файлы.</summary>
    Cancelled,
}

/// <summary>
/// Сохранение одного вида кадра: вид, полный путь, можно ли перезаписать → метаданные записанного кадра
/// (в проект серии; имя файла в них runner заменит на итоговое).
/// Кадр и оверлей по оформлению вида выбирает вызывающий.
/// </summary>
public delegate Task<FrameMetadata> SaveFrameKind(FrameKind kind, string fullPath, bool overwrite);

/// <summary>
/// Логика прогона серии без UI: съёмка шага (обычные виды кадра по шаблону имени одним нажатием, затем
/// дополнительные кадры — по одному на нажатие, их можно пропустить), переход дальше,
/// переключение увеличения, пересъёмка, удаление, пропуск, произвольный номер. Вызывать из одного потока.
/// <para>«Снять без масштаба» (нет калибровки): снимаются только чистые виды, шаг отмечается снятым
/// (Done) с неполным набором файлов — это «частично» (<see cref="IsPartial"/>); его можно переснять позже.</para>
/// </summary>
public sealed class SeriesRunner
{
    private readonly ISeriesEnvironment _env;

    public SeriesRunner(SeriesSession session, ISeriesEnvironment env)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    public SeriesSession Session { get; }
    public SeriesPlan Plan => Session.Plan;
    public SeriesStepKey Current => Session.Current;

    /// <summary>Последний снятый шаг (для Ctrl+Z — «переснять предыдущий»).</summary>
    public SeriesStepKey? LastCaptured { get; private set; }

    /// <summary>Что-то в серии поменялось (статусы, текущий шаг).</summary>
    public event Action? Changed;

    /// <summary>Шагов плана, снятых частично (без видов с масштабом).</summary>
    public int PartialCount => PartialSteps(Session).Count();

    /// <summary>Шагов плана, у которых сняты обычные виды, но остались дополнительные кадры.</summary>
    public int AwaitingExtraCount => AwaitingExtraSteps(Session).Count();

    /// <summary>
    /// Серия пройдена: неснятых шагов нет, нет частично снятых и нет шагов, ждущих дополнительный кадр.
    /// Частичный шаг в файле серии — обычный Done с неполным набором файлов (формат файла тот же).
    /// </summary>
    public static bool IsFinished(SeriesSession s) => s.IsComplete && !PartialSteps(s).Any() && !AwaitingExtraSteps(s).Any();

    /// <summary>Частично снятые шаги плана (не все обычные виды) в порядке обхода.</summary>
    public static IEnumerable<SeriesStepKey> PartialSteps(SeriesSession s) =>
        s.Plan.EnumerateSteps().Where(k => IsPartial(s, k));

    /// <summary>Шаги плана, ждущие дополнительный кадр, в порядке обхода.</summary>
    public static IEnumerable<SeriesStepKey> AwaitingExtraSteps(SeriesSession s) =>
        s.Plan.EnumerateSteps().Where(k => PendingSeparate(s, k).Count > 0);

    /// <summary>Номера видов, файлы которых есть в записи шага (по имени файла без расширения).</summary>
    private static HashSet<int> CapturedNumbers(SeriesSession s, SeriesStepKey k)
    {
        var rec = s.RecordOf(k);
        var names = rec.Files.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return s.Plan.Kinds.Where(kind => names.Contains(Path.GetFileNameWithoutExtension(SeriesNaming.FileName(s.Plan, k, kind, ""))))
            .Select(kind => kind.Number).ToHashSet();
    }

    private static bool IsPartial(SeriesSession s, SeriesStepKey k)
    {
        if (s.RecordOf(k).Status != StepStatus.Done) return false;
        var got = CapturedNumbers(s, k);
        return s.Plan.MainKindsAt(k.Magnification).Any(kind => !got.Contains(kind.Number));
    }

    /// <summary>Дополнительные кадры снятого шага, которые ещё не сняты и не пропущены (в порядке списка).</summary>
    private static IReadOnlyList<FrameKind> PendingSeparate(SeriesSession s, SeriesStepKey k)
    {
        var rec = s.RecordOf(k);
        if (rec.Status != StepStatus.Done) return Array.Empty<FrameKind>();
        var got = CapturedNumbers(s, k);
        var skipped = rec.SkippedKinds ?? Array.Empty<int>();
        return s.Plan.SeparateKindsAt(k.Magnification).Where(kind => !got.Contains(kind.Number) && !skipped.Contains(kind.Number)).ToArray();
    }

    /// <summary>Среди видов кадра есть вид с плашкой масштаба.</summary>
    public bool NeedsScale => Plan.Kinds.Any(k => k.Decoration != FrameDecoration.Clean);

    /// <summary>То, что снимет следующее «Снять шаг» на шаге <paramref name="k"/>, можно снять целиком (калибровка есть или не нужна).</summary>
    public bool CanCaptureFull(SeriesStepKey k) =>
        !NextKinds(k).Any(kind => kind.Decoration != FrameDecoration.Clean) || _env.IsCalibrated(k.Magnification);

    /// <summary>Шаг снят не всеми обычными видами («Снять без масштаба»).</summary>
    public bool IsPartial(SeriesStepKey k) => IsPartial(Session, k);

    /// <summary>
    /// Дополнительный кадр, который снимет следующее «Снять шаг» на шаге <paramref name="k"/>; null — следующее
    /// нажатие снимает обычные виды.
    /// </summary>
    public FrameKind? NextSeparateKind(SeriesStepKey k)
    {
        if (Session.Reshooting && k == Current) return null;
        var pending = PendingSeparate(Session, k);
        return pending.Count > 0 ? pending[0] : null;
    }

    /// <summary>Дополнительные кадры шага, ещё не снятые и не пропущенные.</summary>
    public IReadOnlyList<FrameKind> PendingSeparateKinds(SeriesStepKey k) => PendingSeparate(Session, k);

    /// <summary>
    /// Виды, которые снимет следующее нажатие на шаге: дополнительный кадр, если обычные уже сняты, а он ждёт;
    /// иначе обычные виды увеличения (<paramref name="cleanOnly"/> — только без оформления). На увеличении,
    /// где обычных видов нет, первое нажатие снимает первый дополнительный кадр.
    /// </summary>
    public IReadOnlyList<FrameKind> NextKinds(SeriesStepKey k, bool cleanOnly = false)
    {
        IReadOnlyList<FrameKind> kinds;
        if (NextSeparateKind(k) is { } sep) kinds = new[] { sep };
        else
        {
            kinds = Plan.MainKindsAt(k.Magnification);
            if (kinds.Count == 0 && Plan.SeparateKindsAt(k.Magnification) is { Count: > 0 } all) kinds = new[] { all[0] };
        }
        return cleanOnly ? kinds.Where(x => x.Decoration == FrameDecoration.Clean).ToArray() : kinds;
    }

    /// <summary>Полный путь файла вида шага по шаблону.</summary>
    public string PathOf(SeriesStepKey k, FrameKind kind) =>
        Path.Combine(Session.Folder, SeriesNaming.FileName(Plan, k, kind, _env.FileExtension));

    /// <summary>Полные пути файлов обычных видов шага по шаблону: все или только чистые.</summary>
    public IReadOnlyList<string> PathsFor(SeriesStepKey k, bool cleanOnly = false) =>
        Plan.MainKindsAt(k.Magnification).Where(kind => !cleanOnly || kind.Decoration == FrameDecoration.Clean)
            .Select(kind => PathOf(k, kind)).ToArray();

    /// <summary>Поставить в программе увеличение текущего шага. true — пришлось переключить.</summary>
    public bool SyncMagnification()
    {
        var m = Current.Magnification;
        if (m <= 0 || _env.CurrentMagnification == m) return false;
        _env.CurrentMagnification = m;
        return true;
    }

    /// <summary>
    /// Снять текущий шаг: обычные виды (<paramref name="cleanOnly"/> — только без оформления) или ждущий
    /// дополнительный кадр. Виды пишутся по очереди; ошибка записи пробрасывается, шаг тогда не меняется.
    /// Пока у шага остаются дополнительные кадры, он остаётся текущим.
    /// </summary>
    public async Task<StepCaptureResult> CaptureAsync(SaveFrameKind save, bool cleanOnly = false)
    {
        ArgumentNullException.ThrowIfNull(save);
        var key = Current;
        if (SyncMagnification())
        {
            Changed?.Invoke();
            return StepCaptureResult.MagnificationSwitched;
        }
        if (!cleanOnly && !CanCaptureFull(key)) return StepCaptureResult.NeedsCalibration;

        var addsToStep = NextSeparateKind(key) is not null;
        var kinds = NextKinds(key, cleanOnly);
        if (kinds.Count == 0) return StepCaptureResult.Cancelled;
        var paths = kinds.Select(kind => PathOf(key, kind)).ToArray();
        var overwrite = Session.Reshooting;
        if (!overwrite)
        {
            var existing = paths.Where(_env.FileExists).ToArray();
            if (existing.Length > 0)
            {
                if (!await _env.ConfirmOverwriteAsync(existing)) return StepCaptureResult.Cancelled;
                overwrite = true;
            }
        }

        // Сначала все виды во временные имена; упал любой — всё записанное этой попыткой удаляется,
        // прежние файлы шага не тронуты. Только когда записаны все — переименование в настоящие имена.
        var temps = paths.Select(TempPath).ToArray();
        var frames = new FrameMetadata[paths.Length];
        try
        {
            foreach (var t in temps) _env.DeleteFile(t);   // остатки прошлой неудачи
            var written = 0;
            try
            {
                for (var i = 0; i < kinds.Count; i++)
                {
                    written = i + 1;
                    frames[i] = await save(kinds[i], temps[i], false);
                }
            }
            catch
            {
                for (var i = 0; i < written; i++) _env.DeleteFile(temps[i]);
                throw;
            }
            Commit(paths, temps);
        }
        finally
        {
            var tempRoot = Path.Combine(Session.Folder, TempFolderName);
            _env.DeleteEmptyFolder(Path.Combine(tempRoot, OldFolderName));
            _env.DeleteEmptyFolder(tempRoot);
        }

        // Метаданные — с итоговым именем файла (записывалось во временную папку).
        var newFrames = paths.Select((p, i) => frames[i] with { File = Path.GetFileName(p) }).ToArray();
        var old = Session.RecordOf(key);
        IReadOnlyList<string> files;
        IReadOnlyList<FrameMetadata>? allFrames;
        IReadOnlyList<int>? skipped;
        if (addsToStep)
        {
            // Дополнительный кадр добавляется к уже снятым файлам шага.
            files = old.Files.Where(f => !paths.Contains(f, StringComparer.OrdinalIgnoreCase)).Concat(paths).ToArray();
            var names = newFrames.Select(f => f.File).ToHashSet(StringComparer.OrdinalIgnoreCase);
            allFrames = (old.Frames ?? Array.Empty<FrameMetadata>()).Where(f => !names.Contains(f.File)).Concat(newFrames).ToArray();
            skipped = old.SkippedKinds?.Where(n => !kinds.Any(x => x.Number == n)).ToArray();
        }
        else
        {
            // Обычные виды — новый набор шага. Файлы прежней съёмки, которых в нём нет (пересъёмка «без масштаба»;
            // дополнительные кадры — их снимут заново), удаляются.
            foreach (var f in old.Files)
                if (!paths.Contains(f, StringComparer.OrdinalIgnoreCase)) _env.DeleteFile(f);
            files = paths;
            allFrames = newFrames;
            skipped = null;
        }
        Finish(key, files, allFrames, skipped);
        LastCaptured = key;
        SyncMagnification();
        _env.Persist(Session);
        Changed?.Invoke();
        return StepCaptureResult.Captured;
    }

    /// <summary>
    /// «Пропустить доп. кадр»: ждущий дополнительный кадр текущего шага не снимается. Если других не осталось —
    /// дальше к неснятому. false — текущий шаг не ждёт дополнительного кадра.
    /// </summary>
    public bool SkipSeparate()
    {
        var key = Current;
        if (NextSeparateKind(key) is not { } kind) return false;
        var rec = Session.RecordOf(key);
        Finish(key, rec.Files, rec.Frames, (rec.SkippedKinds ?? Array.Empty<int>()).Append(kind.Number).ToArray());
        AfterMove();
        return true;
    }

    /// <summary>Записать шаг; если дополнительных кадров не осталось — шаг завершён, дальше к неснятому.</summary>
    private void Finish(SeriesStepKey key, IReadOnlyList<string> files, IReadOnlyList<FrameMetadata>? frames, IReadOnlyList<int>? skipped)
    {
        Session.MarkDone(key, files, frames, skipped, complete: false);
        if (PendingSeparate(Session, key).Count > 0) return;
        Session.MarkDone(key, files, frames, skipped, complete: true);
        AdvanceToPartial();
    }

    /// <summary>Переснять шаг: он становится текущим, следующее «Снять шаг» перезапишет его файлы без вопросов.</summary>
    public void Reshoot(SeriesStepKey k)
    {
        Session.BeginReshoot(k);
        AfterMove();
    }

    /// <summary>Ctrl+Z после съёмки: переснять последний снятый шаг. false — ещё ничего не снято.</summary>
    public bool ReshootPrevious()
    {
        if (LastCaptured is not { } k) return false;
        Reshoot(k);
        return true;
    }

    /// <summary>Удалить файлы шага, шаг снова «не снят» (его записи кадров уходят из проекта).</summary>
    public void Delete(SeriesStepKey k)
    {
        foreach (var f in Session.RecordOf(k).Files) _env.DeleteFile(f);
        Session.Reset(k);
        if (LastCaptured == k) LastCaptured = null;
        _env.Persist(Session);
        Changed?.Invoke();
    }

    /// <summary>Пропустить шаг; если он текущий — дальше к неснятому.</summary>
    public void Skip(SeriesStepKey k)
    {
        Session.MarkSkipped(k);
        AdvanceToPartial();
        AfterMove();
    }

    /// <summary>Сделать текущим любой шаг, в том числе с произвольным номером вне плана.</summary>
    public void MakeCurrent(SeriesStepKey k)
    {
        Session.SetCurrent(k);
        AfterMove();
    }

    /// <summary>«◀ ▶»: сдвинуть группу и образец текущего шага (не меньше 1), увеличение то же.</summary>
    public void Move(int dGroup, int dSample)
    {
        var c = Current;
        MakeCurrent(c with { Group = Math.Max(1, c.Group + dGroup), Sample = Math.Max(1, c.Sample + dSample) });
    }

    /// <summary>Папка для записи шага до переименования (имя файла то же, что итоговое).</summary>
    public const string TempFolderName = ".zs-new";

    private static string TempPath(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? "", TempFolderName, Path.GetFileName(path));

    private const string OldFolderName = "old";

    /// <summary>
    /// Поставить записанный набор на место: прежние файлы шага отодвигаются в .zs-new\old,
    /// новые переименовываются в настоящие имена. Любой сбой (файл открыт в другой программе) — все переносы
    /// откатываются в обратном порядке, новые файлы удаляются, на диске ровно прежний набор.
    /// </summary>
    private void Commit(IReadOnlyList<string> paths, string[] temps)
    {
        var oldDir = Path.Combine(Session.Folder, TempFolderName, OldFolderName);
        var moved = new List<(string From, string To)>();
        var busy = paths[0];
        try
        {
            foreach (var f in paths)
            {
                if (!_env.FileExists(f)) continue;
                busy = f;
                var o = Path.Combine(oldDir, Path.GetFileName(f));
                _env.MoveFile(f, o);
                moved.Add((f, o));
            }
            for (var i = 0; i < paths.Count; i++)
            {
                busy = paths[i];
                _env.MoveFile(temps[i], paths[i]);
                moved.Add((temps[i], paths[i]));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            for (var j = moved.Count - 1; j >= 0; j--)
            {
                try { _env.MoveFile(moved[j].To, moved[j].From); }
                catch (Exception) { /* вернуть не удалось — файл останется в .zs-new, но не пропадёт */ }
            }
            foreach (var t in temps) _env.DeleteFile(t);
            throw new SeriesFileBusyException(busy, ex);
        }
        // Прежние файлы больше не нужны.
        foreach (var (_, to) in moved)
            if (to.StartsWith(oldDir, StringComparison.OrdinalIgnoreCase)) _env.DeleteFile(to);
    }

    /// <summary>
    /// Неснятых шагов не осталось, а текущий уже пройден — дальше первый частичный шаг, для увеличения которого
    /// уже есть калибровка (пересъёмкой: его файлы перезапишутся без вопросов).
    /// </summary>
    private void AdvanceToPartial()
    {
        if (Session.NextPending() is not null || Session.RecordOf(Current).Status == StepStatus.Pending) return;
        if (Session.IsInPlan(Current) && IsPartial(Current) && CanCaptureFull(Current) && Session.Reshooting) return;
        if (PendingSeparate(Session, Current).Count > 0) return;
        foreach (var k in PartialSteps(Session))
            if (!Plan.MainKindsAt(k.Magnification).Any(x => x.Decoration != FrameDecoration.Clean) || _env.IsCalibrated(k.Magnification))
            {
                Session.BeginReshoot(k);
                return;
            }
        // Остались шаги без дополнительного кадра (лаборант ушёл с шага, не сняв его) — к первому из них.
        foreach (var k in AwaitingExtraSteps(Session))
        {
            Session.SetCurrent(k);
            return;
        }
    }

    private void AfterMove()
    {
        SyncMagnification();
        _env.Persist(Session);
        Changed?.Invoke();
    }
}

/// <summary>Файл шага занят другой программой (например, открыт в просмотрщике) — шаг не снят, файлы прежние.</summary>
public sealed class SeriesFileBusyException : IOException
{
    public SeriesFileBusyException(string path, Exception inner)
        : base($"Шаг не снят: файл {Path.GetFileName(path)} занят другой программой. Закройте его и снимите снова.", inner) => FilePath = path;

    public string FilePath { get; }
}
