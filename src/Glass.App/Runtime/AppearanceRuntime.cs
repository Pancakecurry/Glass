using Glass.Core.Appearance;
using Glass.Core.Persistence;

namespace Glass.App.Runtime;

internal sealed class AppearanceSettingsRuntime(IGlassSettingsStore store) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public GlassSettings Current { get; private set; } = new();
    public event EventHandler? Changed;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
        Current = await store.LoadAsync(new GlassSettings(), cancellationToken);

    public async ValueTask UpdateAsync(
        Func<GlassSettings, GlassSettings> update,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(update);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Current = update(Current).Normalize();
            await store.SaveAsync(Current, cancellationToken);
        }
        finally { _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ValueTask ApplyPresetAsync(MaterialSettings material,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(settings => settings with
        {
            Appearance = settings.Appearance with { Material = material },
        }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _gate.WaitAsync();
        _gate.Release();
        _gate.Dispose();
        Changed = null;
    }
}
