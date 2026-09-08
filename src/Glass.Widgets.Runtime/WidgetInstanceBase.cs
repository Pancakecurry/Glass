using Glass.Widgets.Abstractions;

namespace Glass.Widgets.Runtime;

public abstract class WidgetInstanceBase : IWidgetInstance
{
    private bool _disposed;

    protected WidgetInstanceBase(WidgetInstanceConfiguration configuration) =>
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public WidgetInstanceConfiguration Configuration { get; }
    public WidgetLifecycleState State { get; private set; } = WidgetLifecycleState.Created;

    public async ValueTask MountAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != WidgetLifecycleState.Created) return;
        await OnMountedAsync(cancellationToken).ConfigureAwait(false);
        State = WidgetLifecycleState.Mounted;
    }

    public async ValueTask SetVisibleAsync(
        bool visible,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State == WidgetLifecycleState.Created)
            await MountAsync(cancellationToken).ConfigureAwait(false);

        var target = visible ? WidgetLifecycleState.Visible : WidgetLifecycleState.Suspended;
        if (State == target) return;
        await OnVisibilityChangedAsync(visible, cancellationToken).ConfigureAwait(false);
        State = target;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await OnDisposedAsync().ConfigureAwait(false);
        State = WidgetLifecycleState.Disposed;
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask OnMountedAsync(CancellationToken token) => ValueTask.CompletedTask;
    protected virtual ValueTask OnVisibilityChangedAsync(bool visible, CancellationToken token) =>
        ValueTask.CompletedTask;
    protected virtual ValueTask OnDisposedAsync() => ValueTask.CompletedTask;
}
