using Glass.Infrastructure.Diagnostics;

namespace Glass.App.Runtime;

internal sealed class SerialDiagnosticWriter(LocalDiagnosticLog log)
{
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;

    public void Enqueue(string category, string message)
    {
        lock (_gate)
        {
            _tail = _tail.ContinueWith(
                    _ => WriteSafelyAsync(category, message),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    public Task FlushAsync()
    {
        lock (_gate)
        {
            return _tail;
        }
    }

    private async Task WriteSafelyAsync(string category, string message)
    {
        try
        {
            await log.WriteAsync(category, message).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Local runtime diagnostic failed: {exception}");
        }
    }
}
