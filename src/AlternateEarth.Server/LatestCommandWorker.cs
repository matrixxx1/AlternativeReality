namespace AlternateEarth.Server;

// One cancellable route per connection. World mutations remain in the socket receive loop.
public sealed class LatestCommandWorker : IAsyncDisposable
{
    private CancellationTokenSource? _current;
    private Task _last = Task.CompletedTask;

    public void Cancel() => _current?.Cancel();

    public void Replace(Func<CancellationToken, Task> work, CancellationToken connectionToken = default)
    {
        Cancel();
        var prior = _last;
        var previousSource = _current;
        var source = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
        _current = source;
        _last = Task.Run(async () =>
        {
            try
            {
                try { await prior; }
                finally { previousSource?.Dispose(); }
                source.Token.ThrowIfCancellationRequested();
                await work(source.Token);
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        }, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        try { await _last; }
        finally { _current?.Dispose(); }
    }
}
