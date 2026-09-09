using Glass.Widgets.Abstractions;

namespace Glass.Widgets.Runtime;

public sealed class ProviderCoordinator : IAsyncDisposable
{
    private readonly Dictionary<string, Entry> _providers = [];
    private int _activeProviderCount;
    private bool _disposed;

    public int RegisteredProviderCount => _providers.Count;
    public int ActiveProviderCount => Volatile.Read(ref _activeProviderCount);

    public void Register(IWidgetProvider provider)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(provider);
        if (!_providers.TryAdd(provider.ProviderId, new Entry(provider)))
        {
            throw new InvalidOperationException($"Provider '{provider.ProviderId}' is already registered.");
        }
    }

    public async ValueTask SetVisibleAsync(
        string providerId,
        WidgetInstanceId consumer,
        bool visible,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_providers.TryGetValue(providerId, out var entry))
        {
            throw new KeyNotFoundException($"Unknown provider '{providerId}'.");
        }

        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wasActive = entry.VisibleConsumers.Count > 0;
            if (visible) entry.VisibleConsumers.Add(consumer);
            else entry.VisibleConsumers.Remove(consumer);
            var isActive = entry.VisibleConsumers.Count > 0;
            if (!wasActive && isActive)
            {
                await entry.Provider.StartAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _activeProviderCount);
            }
            else if (wasActive && !isActive)
            {
                await entry.Provider.StopAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Decrement(ref _activeProviderCount);
            }
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var entry in _providers.Values)
        {
            await entry.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (entry.VisibleConsumers.Count > 0)
                {
                    await entry.Provider.StopAsync().ConfigureAwait(false);
                    Interlocked.Decrement(ref _activeProviderCount);
                }
            }
            finally
            {
                entry.Gate.Release();
                entry.Gate.Dispose();
            }
        }
        _providers.Clear();
    }

    private sealed class Entry(IWidgetProvider provider)
    {
        public IWidgetProvider Provider { get; } = provider;
        public HashSet<WidgetInstanceId> VisibleConsumers { get; } = [];
        public SemaphoreSlim Gate { get; } = new(1, 1);
    }
}
