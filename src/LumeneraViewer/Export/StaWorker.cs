using System.Collections.Concurrent;

namespace LumeneraViewer.Export;

/// <summary>
/// Один фоновый STA-поток с очередью задач: энкодерам WPF и RenderTargetBitmap нужен STA,
/// а UI-поток занимать записью нельзя. Задачи выполняются строго по порядку.
/// </summary>
public sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private int _pending;

    public StaWorker(string name)
    {
        var thread = new Thread(Loop) { IsBackground = true, Name = name };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>Сколько задач ещё не завершено (в очереди и в работе).</summary>
    public int Pending => Volatile.Read(ref _pending);

    public Task<T> Run<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Increment(ref _pending);
        try
        {
            _queue.Add(() =>
            {
                try { tcs.SetResult(work()); }
                catch (Exception ex) { tcs.SetException(ex); }
                finally { Interlocked.Decrement(ref _pending); }
            });
        }
        catch (InvalidOperationException)
        {
            Interlocked.Decrement(ref _pending);
            tcs.SetException(new ObjectDisposedException(nameof(StaWorker)));
        }
        return tcs.Task;
    }

    /// <summary>Дождаться, пока очередь опустеет (например, при закрытии окна). false — не успели.</summary>
    public bool WaitIdle(TimeSpan timeout)
    {
        var until = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Pending > 0)
        {
            if (Environment.TickCount64 > until) return false;
            Thread.Sleep(20);
        }
        return true;
    }

    private void Loop()
    {
        foreach (var work in _queue.GetConsumingEnumerable()) work();
    }

    public void Dispose() => _queue.CompleteAdding();
}
