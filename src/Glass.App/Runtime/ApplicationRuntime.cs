using Glass.App.Widgets;
using Glass.Core.Appearance;
using Glass.Core.Shell;
using Glass.Infrastructure.Diagnostics;
using Glass.Infrastructure.Persistence;
using Glass.Infrastructure.Storage;
using Glass.Platform.Windows.Accessibility;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Audio;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Location;
using Glass.Platform.Windows.Media;
using Glass.Platform.Windows.SystemStatus;
using Glass.Rendering.Materials;
using Glass.Rendering.Motion;
using Glass.Shell.Runtime;
using Glass.Widgets.Abstractions;
using Glass.Widgets.BuiltIn;
using Glass.Widgets.BuiltIn.Weather;
using Glass.Widgets.Runtime;

namespace Glass.App.Runtime;

internal sealed class ApplicationRuntime : IAsyncDisposable
{
    private readonly GlassDataPaths _dataPaths = GlassDataPaths.CreateDefault();
    private readonly SemaphoreSlim _widgetSyncGate = new(1, 1);
    private WindowsDisplayService? _displays;
    private RunningWindowTracker? _runningWindows;
    private ShellIconService? _icons;
    private WidgetSurfaceManager? _widgetSurfaces;
    private WidgetRuntime? _widgetRuntime;
    private ProviderCoordinator? _providerCoordinator;
    private SystemMetricsProvider? _metrics;
    private PowerStatusProvider? _power;
    private AudioEndpointService? _audio;
    private SystemMediaSessionService? _media;
    private MetNorwayWeatherProvider? _weather;
    private HttpClient? _weatherClient;
    private WindowsInteractionPreferences? _interactionPreferences;
    private AppearanceSettingsRuntime? _settings;
    private GlassMotionController? _motion;
    private ShellRuntime? _shell;
    private ControlCenterWindow? _controlCenter;
    private LocalDiagnosticLog? _diagnostics;
    private SerialDiagnosticWriter? _diagnosticWriter;
    private bool _started;
    private bool _disposed;

    public event Action? ShutdownCompleted;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;

        _dataPaths.EnsureCreated();
        _diagnostics = new LocalDiagnosticLog(_dataPaths.LogDirectory);
        _diagnosticWriter = new SerialDiagnosticWriter(_diagnostics);
        var stateStore = new AtomicJsonStateStore(_dataPaths);
        var layoutStore = new JsonShellLayoutStore(stateStore, _diagnostics);
        _settings = new AppearanceSettingsRuntime(
            new JsonGlassSettingsStore(stateStore, _diagnostics));
        await _settings.InitializeAsync(cancellationToken);

        _displays = new WindowsDisplayService();
        _runningWindows = new RunningWindowTracker();
        _icons = new ShellIconService();
        _interactionPreferences = new WindowsInteractionPreferences();
        _motion = new GlassMotionController(
            _settings.Current.Appearance.MotionPreference,
            _interactionPreferences.AnimationsEnabled);
        var materials = new GlassMaterialController();
        var launcher = new ApplicationLaunchService();
        var applications = new WindowsApplicationCatalog();

        _metrics = new SystemMetricsProvider();
        _power = new PowerStatusProvider();
        _audio = new AudioEndpointService();
        _media = new SystemMediaSessionService();
        _weatherClient = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _weather = new MetNorwayWeatherProvider(_weatherClient, stateStore);
        _providerCoordinator = new ProviderCoordinator();
        _providerCoordinator.Register(WidgetProviderAdapters.Metrics(_metrics));
        _providerCoordinator.Register(WidgetProviderAdapters.Power(_power));
        _providerCoordinator.Register(WidgetProviderAdapters.Audio(_audio));
        _providerCoordinator.Register(WidgetProviderAdapters.Media(_media));
        _providerCoordinator.Register(WidgetProviderAdapters.Passive("clipboard"));
        _providerCoordinator.Register(WidgetProviderAdapters.Passive("weather"));

        var widgetState = new WidgetStateStore(stateStore);
        var widgetRegistry = new WidgetRegistry();
        BuiltInWidgetRegistration.RegisterAll(widgetRegistry, _providerCoordinator, widgetState);
        _widgetRuntime = new WidgetRuntime(widgetRegistry);
        var widgetViews = new BuiltInWidgetViewFactory(new WidgetViewServices(
            _media, _metrics, _power, _audio, launcher, _icons, applications, _weather,
            new OneShotLocationService(), () => _settings.Current.Appearance));
        var editMode = new Glass.Core.Editing.EditModeSession();

        ShellRuntime? shellReference = null;
        var surfaceServices = new ProductSurfaceServices(
            () => shellReference ?? throw new InvalidOperationException("Shell is not ready."),
            () => _settings.Current,
            _widgetRuntime,
            widgetViews,
            _icons,
            applications,
            materials,
            _motion,
            editMode,
            () => _controlCenter?.Present(),
            () => _widgetSurfaces?.Sync(_shell!.Layout));
        _shell = shellReference = new ShellRuntime(
            layoutStore,
            _displays,
            new BarSurfaceFactory(_displays, _runningWindows, launcher, surfaceServices),
            stateStore);
        _shell.RuntimeFaulted += OnRuntimeFaulted;
        await _shell.InitializeAsync(cancellationToken);
        await SynchronizeWidgetRuntimeAsync(cancellationToken);
        foreach (var surface in _shell.Surfaces)
        {
            var definition = _shell.Layout.Bars.First(bar => bar.Id == surface.Id);
            surface.Apply(definition);
        }

        _widgetSurfaces = new WidgetSurfaceManager(
            _displays,
            surfaceServices,
            definition => _shell.UpdateStandaloneWidgetAsync(definition).AsTask());
        _widgetSurfaces.Sync(_shell.Layout);
        _shell.LayoutChanged += OnLayoutChanged;
        _settings.Changed += OnSettingsChanged;
        _interactionPreferences.Changed += OnInteractionPreferencesChanged;
        _runningWindows.Start();
        try { applications.Refresh(); }
        catch (Exception exception) { _diagnosticWriter.Enqueue("applications", exception.ToString()); }

        _controlCenter = new ControlCenterWindow(
            _shell, _displays, applications, BuiltInWidgetCatalog.All,
            _settings, editMode, materials,
            () => _widgetSurfaces.Sync(_shell.Layout), ShutdownAsync);
        _controlCenter.Present();
        _started = true;
    }

    public void Activate()
    {
        if (_controlCenter is { } window)
            window.DispatcherQueue.TryEnqueue(window.Present);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_shell is not null)
        {
            _shell.LayoutChanged -= OnLayoutChanged;
            _shell.RuntimeFaulted -= OnRuntimeFaulted;
        }
        if (_settings is not null) _settings.Changed -= OnSettingsChanged;
        if (_interactionPreferences is not null)
            _interactionPreferences.Changed -= OnInteractionPreferencesChanged;

        _widgetSurfaces?.Dispose();
        _widgetSurfaces = null;
        if (_shell is not null)
        {
            await _shell.DisposeAsync();
            _shell = null;
        }
        if (_widgetRuntime is not null)
        {
            await _widgetRuntime.DisposeAsync();
            _widgetRuntime = null;
        }
        if (_providerCoordinator is not null)
        {
            await _providerCoordinator.DisposeAsync();
            _providerCoordinator = null;
        }
        if (_metrics is not null) await _metrics.DisposeAsync();
        _metrics = null;
        _power?.Dispose();
        _power = null;
        _audio?.Dispose();
        _audio = null;
        _media?.Dispose();
        _media = null;
        _weather?.Dispose();
        _weather = null;
        _weatherClient?.Dispose();
        _weatherClient = null;
        if (_settings is not null) await _settings.DisposeAsync();
        _settings = null;
        _interactionPreferences?.Dispose();
        _interactionPreferences = null;
        _displays?.Dispose();
        _displays = null;
        _runningWindows?.Dispose();
        _runningWindows = null;
        _icons?.Dispose();
        _icons = null;
        _controlCenter = null;
        await _widgetSyncGate.WaitAsync();
        _widgetSyncGate.Release();
        _widgetSyncGate.Dispose();
        if (_diagnosticWriter is not null)
        {
            await _diagnosticWriter.FlushAsync();
            _diagnosticWriter = null;
        }
        _diagnostics?.Dispose();
        _diagnostics = null;
        ShutdownCompleted = null;
    }

    private async void OnLayoutChanged(object? sender, EventArgs args)
    {
        try
        {
            await SynchronizeWidgetRuntimeAsync();
            if (_shell is null) return;
            foreach (var surface in _shell.Surfaces)
            {
                var definition = _shell.Layout.Bars.First(bar => bar.Id == surface.Id);
                surface.Apply(definition);
            }
            _widgetSurfaces?.Sync(_shell.Layout);
        }
        catch (Exception exception) { OnRuntimeFaulted(exception); }
    }

    private async Task SynchronizeWidgetRuntimeAsync(CancellationToken cancellationToken = default)
    {
        if (_shell is null || _widgetRuntime is null) return;
        await _widgetSyncGate.WaitAsync(cancellationToken);
        try
        {
            var desired = _shell.Layout.WidgetInstances.ToDictionary(
                widget => new WidgetInstanceId(widget.WidgetInstanceId));
            foreach (var existing in _widgetRuntime.Instances.ToArray())
            {
                if (!desired.TryGetValue(existing.Configuration.InstanceId, out var definition) ||
                    !ConfigurationMatches(existing.Configuration, definition))
                    await _widgetRuntime.RemoveAsync(existing.Configuration.InstanceId);
            }
            foreach (var pair in desired)
            {
                if (_widgetRuntime.TryGet(pair.Key, out _)) continue;
                var widget = pair.Value;
                await _widgetRuntime.CreateAsync(new WidgetInstanceConfiguration(
                    pair.Key,
                    new WidgetTypeId(widget.WidgetTypeId),
                    new WidgetSize(widget.Size.Width, widget.Size.Height),
                    widget.Configuration), cancellationToken);
            }
        }
        finally { _widgetSyncGate.Release(); }
    }

    private static bool ConfigurationMatches(
        WidgetInstanceConfiguration runtime,
        WidgetInstanceDefinition definition) =>
        runtime.TypeId.Value == definition.WidgetTypeId &&
        runtime.Size.Width == definition.Size.Width &&
        runtime.Size.Height == definition.Size.Height &&
        runtime.Settings.Count == definition.Configuration.Count &&
        runtime.Settings.All(pair =>
            definition.Configuration.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private void OnSettingsChanged(object? sender, EventArgs args)
    {
        if (_settings is null || _motion is null || _shell is null) return;
        _motion.Preference = _settings.Current.Appearance.MotionPreference;
        foreach (var surface in _shell.Surfaces)
            surface.Apply(surface.Definition);
        _widgetSurfaces?.Sync(_shell.Layout);
    }

    private void OnInteractionPreferencesChanged(object? sender, EventArgs args)
    {
        if (_interactionPreferences is null || _motion is null) return;
        _motion.SystemAnimationsEnabled = _interactionPreferences.AnimationsEnabled;
        OnSettingsChanged(sender, args);
    }

    private async Task ShutdownAsync()
    {
        var handler = ShutdownCompleted;
        await DisposeAsync();
        handler?.Invoke();
    }

    private void OnRuntimeFaulted(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine($"Glass runtime failure: {exception}");
        _diagnosticWriter?.Enqueue("runtime", exception.ToString());
    }
}
