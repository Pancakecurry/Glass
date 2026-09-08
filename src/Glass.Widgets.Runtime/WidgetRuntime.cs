using Glass.Widgets.Abstractions;

namespace Glass.Widgets.Runtime;

public sealed class WidgetRuntime : IAsyncDisposable
{
    private readonly WidgetRegistry _registry;
    private readonly Dictionary<WidgetInstanceId, IWidgetInstance> _instances = [];
    private bool _disposed;

    public WidgetRuntime(WidgetRegistry registry) =>
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public IReadOnlyCollection<IWidgetInstance> Instances => _instances.Values;

    public bool TryGet(WidgetInstanceId id, out IWidgetInstance? instance) =>
        _instances.TryGetValue(id, out instance);

    public async ValueTask<IWidgetInstance> CreateAsync(
        WidgetInstanceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_instances.ContainsKey(configuration.InstanceId))
            throw new InvalidOperationException($"Widget '{configuration.InstanceId}' already exists.");

        var instance = _registry.Create(configuration);
        _instances.Add(configuration.InstanceId, instance);
        try
        {
            await instance.MountAsync(cancellationToken).ConfigureAwait(false);
            return instance;
        }
        catch
        {
            _instances.Remove(configuration.InstanceId);
            await instance.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask SetVisibleAsync(
        WidgetInstanceId id,
        bool visible,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instances.TryGetValue(id, out var instance)
            ? instance.SetVisibleAsync(visible, cancellationToken)
            : ValueTask.CompletedTask;
    }

    public async ValueTask RemoveAsync(WidgetInstanceId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_instances.Remove(id, out var instance))
            await instance.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var instance in _instances.Values)
            await instance.DisposeAsync().ConfigureAwait(false);
        _instances.Clear();
    }
}
