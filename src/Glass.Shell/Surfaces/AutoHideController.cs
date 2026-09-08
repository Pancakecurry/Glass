namespace Glass.Shell.Surfaces;

public enum AutoHideState
{
    Visible,
    PendingHide,
    Hidden,
    Revealing,
}

public sealed class AutoHideController : IDisposable
{
    private readonly TimeSpan _hideDelay;
    private CancellationTokenSource? _pendingHide;
    private bool _enabled;
    private bool _disposed;

    public AutoHideController(TimeSpan? hideDelay = null)
    {
        _hideDelay = hideDelay ?? TimeSpan.FromMilliseconds(650);
    }

    public AutoHideState State { get; private set; } = AutoHideState.Visible;

    public event Action<AutoHideState>? StateChanged;

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _enabled = enabled;
        if (!enabled)
        {
            CancelPendingHide();
            Transition(AutoHideState.Visible);
        }
    }

    public void PointerEntered()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelPendingHide();
        if (!_enabled || State == AutoHideState.Visible)
        {
            return;
        }

        Transition(AutoHideState.Revealing);
        Transition(AutoHideState.Visible);
    }

    public void PointerExited()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_enabled || State != AutoHideState.Visible)
        {
            return;
        }

        CancelPendingHide();
        _pendingHide = new CancellationTokenSource();
        Transition(AutoHideState.PendingHide);
        _ = HideAfterDelayAsync(_pendingHide.Token);
    }

    public void HideImmediately()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_enabled)
        {
            CancelPendingHide();
            Transition(AutoHideState.Hidden);
        }
    }

    public void ShowImmediately()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelPendingHide();
        Transition(AutoHideState.Visible);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingHide();
        StateChanged = null;
    }

    private async Task HideAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_hideDelay, cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                Transition(AutoHideState.Hidden);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void CancelPendingHide()
    {
        _pendingHide?.Cancel();
        _pendingHide?.Dispose();
        _pendingHide = null;
    }

    private void Transition(AutoHideState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(state);
    }
}
