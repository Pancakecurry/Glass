using Glass.Core.Persistence;
using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Shell.Surfaces;

namespace Glass.Shell.Runtime;

public sealed class ShellRuntime : IAsyncDisposable
{
    private readonly IShellLayoutStore _store;
    private readonly WindowsDisplayService _displays;
    private readonly IBarSurfaceFactory _surfaceFactory;
    private readonly Dictionary<BarId, IBarSurface> _surfaces = [];
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _initialized;
    private bool _disposed;

    public ShellRuntime(
        IShellLayoutStore store,
        WindowsDisplayService displays,
        IBarSurfaceFactory surfaceFactory)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _displays = displays ?? throw new ArgumentNullException(nameof(displays));
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
    }

    public ShellLayout Layout { get; private set; } = new([]);

    public IReadOnlyCollection<IBarSurface> Surfaces => _surfaces.Values;

    public event Action<Exception>? RuntimeFaulted;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        var fallback = ShellLayout.CreateDefault(
            WindowsDisplayService.ToTarget(_displays.PrimaryDisplay));
        Layout = (await _store.LoadAsync(fallback, cancellationToken)).Normalize();
        foreach (var definition in Layout.Bars.Where(bar => bar.IsEnabled))
        {
            CreateSurface(definition);
        }

        _displays.DisplaysChanged += OnDisplaysChanged;
        _initialized = true;
    }

    public async ValueTask<BarDefinition> CreateBarAsync(
        DisplayTarget target,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var definition = BarDefinition.CreateDefault(target);
        Layout = new ShellLayout([.. Layout.Bars, definition]);
        CreateSurface(definition);
        await SaveAsync(cancellationToken);
        return definition;
    }

    public async ValueTask UpdateBarAsync(
        BarDefinition definition,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        definition = definition.Normalize();
        var index = Layout.Bars.ToList().FindIndex(bar => bar.Id == definition.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Unknown bar {definition.Id}.");
        }

        var bars = Layout.Bars.ToArray();
        bars[index] = definition;
        Layout = new ShellLayout(bars);
        if (_surfaces.TryGetValue(definition.Id, out var surface))
        {
            if (definition.IsEnabled)
            {
                surface.Apply(definition);
                if (surface.Definition != definition)
                {
                    bars[index] = surface.Definition;
                    Layout = new ShellLayout(bars);
                }

                if (surface.LastFailure is { } failure)
                {
                    RuntimeFaulted?.Invoke(failure);
                }
            }
            else
            {
                RemoveSurface(definition.Id);
            }
        }
        else if (definition.IsEnabled)
        {
            CreateSurface(definition);
        }

        await SaveAsync(cancellationToken);
    }

    public async ValueTask RemoveBarAsync(
        BarId id,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (!Layout.Bars.Any(bar => bar.Id == id))
        {
            return;
        }

        RemoveSurface(id);
        Layout = new ShellLayout(Layout.Bars.Where(bar => bar.Id != id).ToArray());
        await SaveAsync(cancellationToken);
    }

    public void SetBarVisible(BarId id, bool visible)
    {
        EnsureInitialized();
        if (_surfaces.TryGetValue(id, out var surface))
        {
            surface.SetVisible(visible);
        }
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        foreach (var id in _surfaces.Keys.ToArray())
        {
            RemoveSurface(id);
        }

        Layout = ShellLayout.CreateDefault(
            WindowsDisplayService.ToTarget(_displays.PrimaryDisplay));
        CreateSurface(Layout.Bars[0]);
        await SaveAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_initialized)
        {
            await SaveAsync(CancellationToken.None);
        }

        _lifetime.Cancel();
        _displays.DisplaysChanged -= OnDisplaysChanged;
        foreach (var id in _surfaces.Keys.ToArray())
        {
            RemoveSurface(id);
        }

        await _saveGate.WaitAsync();
        _saveGate.Release();
        _saveGate.Dispose();
        _lifetime.Dispose();
        RuntimeFaulted = null;
    }

    private void CreateSurface(BarDefinition definition)
    {
        var surface = _surfaceFactory.Create(definition);
        surface.DefinitionSettled += OnDefinitionSettled;
        _surfaces.Add(definition.Id, surface);
        if (surface.Definition != definition)
        {
            Layout = new ShellLayout(Layout.Bars
                .Select(bar => bar.Id == definition.Id ? surface.Definition : bar)
                .ToArray());
        }

        if (surface.LastFailure is { } failure)
        {
            RuntimeFaulted?.Invoke(failure);
        }

        surface.SetVisible(true);
    }

    private void RemoveSurface(BarId id)
    {
        if (!_surfaces.Remove(id, out var surface))
        {
            return;
        }

        surface.DefinitionSettled -= OnDefinitionSettled;
        surface.Close();
        surface.Dispose();
    }

    private async void OnDefinitionSettled(
        object? sender,
        BarDefinitionChangedEventArgs args)
    {
        try
        {
            var bars = Layout.Bars
                .Select(bar => bar.Id == args.Definition.Id ? args.Definition : bar)
                .ToArray();
            Layout = new ShellLayout(bars);
            await SaveAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RuntimeFaulted?.Invoke(exception);
        }
    }

    private async void OnDisplaysChanged(object? sender, EventArgs args)
    {
        try
        {
            var changed = false;
            var definitions = Layout.Bars.ToDictionary(bar => bar.Id);
            foreach (var surface in _surfaces.Values)
            {
                var reconciled = surface.ReconcileDisplay();
                changed |= definitions[reconciled.Id] != reconciled;
                definitions[reconciled.Id] = reconciled;
            }

            if (changed)
            {
                Layout = new ShellLayout(Layout.Bars.Select(bar => definitions[bar.Id]).ToArray());
                await SaveAsync(_lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RuntimeFaulted?.Invoke(exception);
        }
    }

    private async ValueTask SaveAsync(CancellationToken cancellationToken)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await _store.SaveAsync(Layout, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                RuntimeFaulted?.Invoke(exception);
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new InvalidOperationException("Shell runtime has not been initialized.");
        }
    }
}
