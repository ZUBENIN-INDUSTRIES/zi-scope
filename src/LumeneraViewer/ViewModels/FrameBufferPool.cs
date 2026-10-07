using LumeneraViewer.Camera;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Пул буферов обработанных кадров: конвейер не выделяет 8-МБ массив на каждый кадр.
/// Правило владения: у кадра из пула есть счётчик ссылок. Буфер возвращается в пул только
/// когда счётчик дошёл до нуля; «сбежавший» кадр (Escape) из учёта выводится и не переиспользуется никогда.
/// Кадры не из пула (AddRef → false) неизменяемы и живут, пока на них есть ссылка.
/// </summary>
internal sealed class FrameBufferPool
{
    public const int MaxFree = 3;

    private readonly object _lock = new();
    private readonly Dictionary<CameraFrame, int> _refs = new(ReferenceEqualityComparer.Instance);
    private readonly Stack<byte[]> _free = new();

    /// <summary>Новый кадр со счётчиком 1 (буфер из пула или новый).</summary>
    public CameraFrame Rent(int width, int height, long sequence, DateTime timestamp)
    {
        var size = width * height * 3;
        byte[]? buf = null;
        lock (_lock)
        {
            while (_free.Count > 0 && buf is null)
            {
                var b = _free.Pop();
                if (b.Length == size) buf = b;   // буферы другого размера (сменилось разрешение) выбрасываем
            }
        }
        var f = new CameraFrame { Bgr24 = buf ?? new byte[size], Width = width, Height = height, Sequence = sequence, Timestamp = timestamp };
        lock (_lock) _refs[f] = 1;
        return f;
    }

    /// <summary>+1 ссылка. false — кадр не из пула или уже «сбежал»: он и так не будет перезаписан.</summary>
    public bool AddRef(CameraFrame? f)
    {
        if (f is null) return false;
        lock (_lock)
        {
            if (!_refs.TryGetValue(f, out var n)) return false;
            _refs[f] = n + 1;
            return true;
        }
    }

    /// <summary>−1 ссылка; на нуле буфер уходит в пул.</summary>
    public void Release(CameraFrame? f)
    {
        if (f is null) return;
        lock (_lock)
        {
            if (!_refs.TryGetValue(f, out var n)) return;
            if (n > 1) { _refs[f] = n - 1; return; }
            _refs.Remove(f);
            if (_free.Count < MaxFree) _free.Push(f.Bgr24);
        }
    }

    /// <summary>Кадр отдан наружу без учёта ссылок — больше никогда не переиспользуется.</summary>
    public void Escape(CameraFrame? f)
    {
        if (f is null) return;
        lock (_lock) _refs.Remove(f);
    }

    /// <summary>Обёртка «отпустить при Dispose».</summary>
    public IDisposable Lease(CameraFrame f) => AddRef(f) ? new LeaseToken(this, f) : NoLease.Instance;

    private sealed class LeaseToken(FrameBufferPool pool, CameraFrame frame) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) pool.Release(frame);
        }
    }

    private sealed class NoLease : IDisposable
    {
        public static readonly NoLease Instance = new();
        public void Dispose() { }
    }
}

/// <summary>
/// Троттлинг сохранения настроек: сериализация не чаще одного тика таймера, последнее изменение не теряется.
/// Не потокобезопасен — вызывать из одного (UI) потока.
/// </summary>
internal sealed class SaveThrottle(Action save)
{
    private bool _dirty, _armed;

    /// <summary>Отметить изменение. true — таймер не взведён, его нужно запустить.</summary>
    public bool Request()
    {
        _dirty = true;
        if (_armed) return false;
        _armed = true;
        return true;
    }

    /// <summary>Тик таймера: сохранить, если были изменения.</summary>
    public void OnTimer()
    {
        _armed = false;
        if (!_dirty) return;
        _dirty = false;
        save();
    }

    /// <summary>Сохранить немедленно, если есть несохранённое (закрытие окна).</summary>
    public void Flush() => OnTimer();
}
