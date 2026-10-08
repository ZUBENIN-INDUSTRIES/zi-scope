using System.IO;
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
    /// <summary>Записать состояние серии (series.zscope-series.json).</summary>
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
/// Сохранение одного вида кадра: вид, полный путь, можно ли перезаписать → записанный путь.
/// Кадр и оверлей по оформлению вида выбирает вызывающий.
/// </summary>
public delegate Task<string> SaveFrameKind(FrameKind kind, string fullPath, bool overwrite);

/// <summary>
/// Логика прогона серии без UI: съёмка шага (все виды кадра по шаблону имени), переход дальше,
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

    /// <summary>
    /// Серия пройдена: неснятых шагов нет и нет частично снятых. Частичный шаг в файле серии — обычный Done
    /// с неполным набором файлов (формат файла тот же).
    /// </summary>
    public static bool IsFinished(SeriesSession s) => s.IsComplete && !PartialSteps(s).Any();

    /// <summary>Частично снятые шаги плана в порядке обхода.</summary>
    public static IEnumerable<SeriesStepKey> PartialSteps(SeriesSession s) =>
        s.Plan.EnumerateSteps().Where(k => s.RecordOf(k) is { Status: StepStatus.Done } r && r.Files.Count < s.Plan.Kinds.Count);

    /// <summary>Среди видов кадра есть вид с плашкой масштаба.</summary>
    public bool NeedsScale => Plan.Kinds.Any(k => k.Decoration != FrameDecoration.Clean);

    /// <summary>Шаг можно снять всеми видами (калибровка есть или она не нужна).</summary>
    public bool CanCaptureFull(SeriesStepKey k) => !NeedsScale || _env.IsCalibrated(k.Magnification);

    /// <summary>Шаг снят не всеми видами («Снять без масштаба»).</summary>
    public bool IsPartial(SeriesStepKey k) =>
        Session.RecordOf(k) is { Status: StepStatus.Done } r && r.Files.Count < Plan.Kinds.Count;

    /// <summary>Виды кадра для съёмки: все или только чистые.</summary>
    public IReadOnlyList<FrameKind> KindsFor(bool cleanOnly) =>
        cleanOnly ? Plan.Kinds.Where(k => k.Decoration == FrameDecoration.Clean).ToArray() : Plan.Kinds;

    /// <summary>Полные пути файлов шага по шаблону: все виды или только чистые.</summary>
    public IReadOnlyList<string> PathsFor(SeriesStepKey k, bool cleanOnly = false) =>
        KindsFor(cleanOnly).Select(kind => Path.Combine(Session.Folder, SeriesNaming.FileName(Plan, k, kind, _env.FileExtension))).ToArray();

    /// <summary>Поставить в программе увеличение текущего шага. true — пришлось переключить.</summary>
    public bool SyncMagnification()
    {
        var m = Current.Magnification;
        if (m <= 0 || _env.CurrentMagnification == m) return false;
        _env.CurrentMagnification = m;
        return true;
    }

    /// <summary>
    /// Снять текущий шаг: <paramref name="cleanOnly"/> — только виды без оформления. Виды пишутся по очереди;
    /// ошибка записи пробрасывается, шаг тогда не отмечается.
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

        var kinds = KindsFor(cleanOnly);
        if (kinds.Count == 0) return StepCaptureResult.Cancelled;
        var paths = PathsFor(key, cleanOnly);
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
        try
        {
            foreach (var t in temps) DeleteWithJson(t);   // остатки прошлой неудачи
            var written = 0;
            try
            {
                for (var i = 0; i < kinds.Count; i++)
                {
                    written = i + 1;
                    await save(kinds[i], temps[i], false);
                }
            }
            catch
            {
                for (var i = 0; i < written; i++) DeleteWithJson(temps[i]);
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
        // Файлы прежней съёмки шага, которых нет в новом наборе (пересъёмка «без масштаба»), — удалить.
        foreach (var f in Session.RecordOf(key).Files)
            if (!paths.Contains(f, StringComparer.OrdinalIgnoreCase)) DeleteWithJson(f);

        Session.MarkDone(key, paths.ToArray());
        LastCaptured = key;
        AdvanceToPartial();
        SyncMagnification();
        _env.Persist(Session);
        Changed?.Invoke();
        return StepCaptureResult.Captured;
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

    /// <summary>Удалить файлы шага (и .json рядом), шаг снова «не снят».</summary>
    public void Delete(SeriesStepKey k)
    {
        foreach (var f in Session.RecordOf(k).Files)
        {
            _env.DeleteFile(f);
            _env.DeleteFile(Path.ChangeExtension(f, ".json"));
        }
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

    /// <summary>Папка для записи шага до переименования (имя файла то же — в .json попадает настоящее имя).</summary>
    public const string TempFolderName = ".zs-new";

    private static string TempPath(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? "", TempFolderName, Path.GetFileName(path));

    private const string OldFolderName = "old";

    private static string JsonOf(string path) => Path.ChangeExtension(path, ".json");

    /// <summary>
    /// Поставить записанный набор на место: прежние файлы шага (снимки и .json) отодвигаются в .zs-new\old,
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
            foreach (var f in paths.SelectMany(p => new[] { p, JsonOf(p) }))
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
                if (_env.FileExists(JsonOf(temps[i])))
                {
                    busy = JsonOf(paths[i]);
                    _env.MoveFile(JsonOf(temps[i]), JsonOf(paths[i]));
                    moved.Add((JsonOf(temps[i]), JsonOf(paths[i])));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            for (var j = moved.Count - 1; j >= 0; j--)
            {
                try { _env.MoveFile(moved[j].To, moved[j].From); }
                catch (Exception) { /* вернуть не удалось — файл останется в .zs-new, но не пропадёт */ }
            }
            foreach (var t in temps) DeleteWithJson(t);
            throw new SeriesFileBusyException(busy, ex);
        }
        // Прежние файлы (в том числе старые .json, к новым снимкам не относящиеся) больше не нужны.
        foreach (var (_, to) in moved)
            if (to.StartsWith(oldDir, StringComparison.OrdinalIgnoreCase)) _env.DeleteFile(to);
    }

    private void DeleteWithJson(string path)
    {
        _env.DeleteFile(path);
        _env.DeleteFile(Path.ChangeExtension(path, ".json"));
    }

    /// <summary>
    /// Неснятых шагов не осталось, а текущий уже пройден — дальше первый частичный шаг, для увеличения которого
    /// уже есть калибровка (пересъёмкой: его файлы перезапишутся без вопросов).
    /// </summary>
    private void AdvanceToPartial()
    {
        if (Session.NextPending() is not null || Session.RecordOf(Current).Status == StepStatus.Pending) return;
        if (Session.IsInPlan(Current) && IsPartial(Current) && CanCaptureFull(Current) && Session.Reshooting) return;
        foreach (var k in PartialSteps(Session))
            if (CanCaptureFull(k)) { Session.BeginReshoot(k); return; }
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
