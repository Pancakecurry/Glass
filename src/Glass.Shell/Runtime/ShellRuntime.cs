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
    private readonly ILocalStateStore? _stateStore;
    private readonly Dictionary<BarId, IBarSurface> _surfaces = [];
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _initialized;
    private bool _disposed;

    public ShellRuntime(
        IShellLayoutStore store,
        WindowsDisplayService displays,
        IBarSurfaceFactory surfaceFactory,
        ILocalStateStore? stateStore = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _displays = displays ?? throw new ArgumentNullException(nameof(displays));
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        _stateStore = stateStore;
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
        Layout = CopyWithBars([.. Layout.Bars, definition]);
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
        Layout = CopyWithBars(bars);
        if (_surfaces.TryGetValue(definition.Id, out var surface))
        {
            if (definition.IsEnabled)
            {
                surface.Apply(definition);
                if (surface.Definition != definition)
                {
                    bars[index] = surface.Definition;
                    Layout = CopyWithBars(bars);
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
        Layout = CopyWithBars(Layout.Bars.Where(bar => bar.Id != id).ToArray());
        await SaveAsync(cancellationToken);
    }

    public async ValueTask AddWidgetToBarAsync(
        WidgetInstanceDefinition widget,
        BarId barId,
        BarZone zone,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (!Layout.Bars.Any(bar => bar.Id == barId))
            throw new KeyNotFoundException($"Unknown bar {barId}.");
        RemoveWidgetHost(widget.WidgetInstanceId);
        var bars = Layout.Bars.Select(bar => bar.Id == barId
            ? bar with { Content = [.. bar.Content, new WidgetBarItem(zone, widget.WidgetInstanceId)] }
            : bar).ToArray();
        Layout = new ShellLayout(bars)
        {
            StandaloneWidgets = Layout.StandaloneWidgets,
            WidgetInstances = [.. Layout.WidgetInstances.Where(item =>
                item.WidgetInstanceId != widget.WidgetInstanceId), widget],
        }.Normalize();
        ApplyContentChanges();
        await SaveAsync(cancellationToken);
    }

    public async ValueTask AddStandaloneWidgetAsync(
        WidgetInstanceDefinition widget,
        SurfacePlacement placement,
        SurfaceZOrder zOrder,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        RemoveWidgetHost(widget.WidgetInstanceId);
        Layout = new ShellLayout(Layout.Bars)
        {
            StandaloneWidgets = [.. Layout.StandaloneWidgets,
                new StandaloneWidgetDefinition(widget.WidgetInstanceId, placement,
                    widget.Size, zOrder, true)],
            WidgetInstances = [.. Layout.WidgetInstances.Where(item =>
                item.WidgetInstanceId != widget.WidgetInstanceId), widget],
        }.Normalize();
        ApplyContentChanges();
        await SaveAsync(cancellationToken);
    }

    public async ValueTask RemoveWidgetAsync(
        Guid widgetInstanceId,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        RemoveWidgetHost(widgetInstanceId);
        Layout = new ShellLayout(Layout.Bars)
        {
            StandaloneWidgets = Layout.StandaloneWidgets,
            WidgetInstances = Layout.WidgetInstances.Where(widget =>
                widget.WidgetInstanceId != widgetInstanceId).ToArray(),
        }.Normalize();
        ApplyContentChanges();
        await SaveAsync(cancellationToken);
        if (_stateStore is not null)
            await _stateStore.DeleteAsync($"widget-{widgetInstanceId:N}", cancellationToken);
    }

    public async ValueTask UpdateStandaloneWidgetAsync(
        StandaloneWidgetDefinition definition,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (!Layout.StandaloneWidgets.Any(widget =>
            widget.WidgetInstanceId == definition.WidgetInstanceId))
            throw new KeyNotFoundException($"Unknown standalone widget {definition.WidgetInstanceId}.");
        Layout = new ShellLayout(Layout.Bars)
        {
            StandaloneWidgets = Layout.StandaloneWidgets.Select(widget =>
                widget.WidgetInstanceId == definition.WidgetInstanceId ? definition : widget).ToArray(),
            WidgetInstances = Layout.WidgetInstances.Select(widget =>
                widget.WidgetInstanceId == definition.WidgetInstanceId
                    ? widget with { Size = definition.Size }
                    : widget).ToArray(),
        }.Normalize();
        await SaveAsync(cancellationToken);
    }

    public async ValueTask PinApplicationAsync(
        BarId barId,
        Glass.Core.Applications.ApplicationIdentity application,
        BarZone zone,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var bars = Layout.Bars.Select(bar => bar.Id == barId &&
            !bar.Content.OfType<PinnedApplicationBarItem>().Any(item => item.Application == application)
                ? bar with { Content = [.. bar.Content, new PinnedApplicationBarItem(zone, application)] }
                : bar).ToArray();
        Layout = new ShellLayout(bars)
        {
            StandaloneWidgets = Layout.StandaloneWidgets,
            WidgetInstances = Layout.WidgetInstances,
        }.Normalize();
        ApplyContentChanges();
        await SaveAsync(cancellationToken);
    }

    public async ValueTask UnpinApplicationAsync(
        BarId barId,
        Glass.Core.Applications.ApplicationIdentity application,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var bars = Layout.Bars.Select(bar => bar.Id == barId
            ? bar with { Content = bar.Content.Where(item =>
                item is not PinnedApplicationBarItem pinned || pinned.Application != application).ToArray() }
            : bar).ToArray();
        Layout = new ShellLayout(bars)
        {
            StandaloneWidgets = Layout.StandaloneWidgets,
            WidgetInstances = Layout.WidgetInstances,
        }.Normalize();
        ApplyContentChanges();
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
            Layout = CopyWithBars(Layout.Bars
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
            Layout = CopyWithBars(bars);
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
                var reconciled = await surface.ReconcileDisplayAsync();
                changed |= definitions[reconciled.Id] != reconciled;
                definitions[reconciled.Id] = reconciled;
            }

            if (changed)
            {
                Layout = CopyWithBars(Layout.Bars.Select(bar => definitions[bar.Id]).ToArray());
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

    private void RemoveWidgetHost(Guid instanceId)
    {
        Layout = new ShellLayout(Layout.Bars.Select(bar => bar with
        {
            Content = bar.Content.Where(item =>
                item is not WidgetBarItem widget || widget.WidgetInstanceId != instanceId).ToArray(),
        }).ToArray())
        {
            StandaloneWidgets = Layout.StandaloneWidgets.Where(widget =>
                widget.WidgetInstanceId != instanceId).ToArray(),
            WidgetInstances = Layout.WidgetInstances,
        };
    }

    private void ApplyContentChanges()
    {
        foreach (var surface in _surfaces.Values)
        {
            var definition = Layout.Bars.FirstOrDefault(bar => bar.Id == surface.Id);
            if (definition is not null) surface.Apply(definition);
        }
    }

    private ShellLayout CopyWithBars(IReadOnlyList<BarDefinition> bars) => new(bars)
    {
        StandaloneWidgets = Layout.StandaloneWidgets,
        WidgetInstances = Layout.WidgetInstances,
    };
}
