using System.Collections.Concurrent;

namespace Pointly.App.Automation;

/// <summary>A cancelled caller releases no native execution slot until the synchronous call really ends.</summary>
public sealed class UiaWorkScheduler : IDisposable
{
    public static UiaWorkScheduler Shared { get; } = new();
    private readonly BlockingCollection<Action> _queue = new(2);
    private readonly Thread _worker;
    private bool _disposed;

    public UiaWorkScheduler()
    {
        _worker = new Thread(() =>
        {
            foreach (Action work in _queue.GetConsumingEnumerable()) work();
        }) { IsBackground = true, Name = "Pindo UIA MTA" };
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }

    public async Task<T> RunAsync<T>(Func<T> action, CancellationToken token, int timeoutMs = 5000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(timeoutMs);
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = timeout.Token.Register(() => result.TrySetCanceled(timeout.Token));
        if (!_queue.TryAdd(() =>
        {
            if (result.Task.IsCompleted) return;
            try { result.TrySetResult(action()); }
            catch (Exception ex) { result.TrySetException(ex); }
        })) throw new InvalidOperationException("UiaWorkerBusy");
        try { return await result.Task; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TimeoutException("UiaCallTimedOut"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        // Never Join a hung cross-process COM provider on the UI thread. No replacement worker is created.
    }
}
