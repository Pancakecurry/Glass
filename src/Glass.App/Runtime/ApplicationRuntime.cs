using Glass.App.Widgets;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Runtime;
using Glass.Core.Shell;
using Glass.Infrastructure.Diagnostics;
using Glass.Infrastructure.Persistence;
using Glass.Infrastructure.Recovery;
using Glass.Infrastructure.Storage;
using Glass.Platform.Windows.Accessibility;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Audio;
using Glass.Platform.Windows.Clipboard;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Location;
using Glass.Platform.Windows.Media;
using Glass.Platform.Windows.Runtime;
using Glass.Platform.Windows.SystemStatus;
using Glass.Platform.Windows.Windowing;
using Glass.Rendering.Materials;
using Glass.Rendering.Motion;
using Glass.Shell.Runtime;
using Glass.Shell.Surfaces;
using Glass.Widgets.Abstractions;
using Glass.Widgets.BuiltIn;
using Glass.Widgets.BuiltIn.Weather;
using Glass.Widgets.Runtime;

namespace Glass.App.Runtime;

internal sealed class ApplicationRuntime : IAsyncDisposable
{
    private readonly GlassDataPaths _dataPaths;
    private readonly StartupActivation _activation;
    private readonly Func<bool, Task> _restartApplication;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private readonly System.Diagnostics.Stopwatch _startupClock =
        System.Diagnostics.Stopwatch.StartNew();
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
    private ClipboardService? _clipboard;
    private SystemMediaSessionService? _media;
    private LazyWeatherProvider? _weather;
    private WindowsInteractionPreferences? _interactionPreferences;
    private WindowsRenderingEnvironment? _renderingEnvironment;
    private FullscreenWindowMonitor? _fullscreen;
    private RenderingPolicy _renderingPolicy = RenderingPolicy.Resolve(
        RenderingQualityPreference.Balanced,
        new RenderingEnvironment(false, true, true, false, false, true));
    private SessionHealthStore? _sessionHealth;
    private PackagedStartupTaskService? _startupTask;
    private AppearanceSettingsRuntime? _settings;
    private GlassMotionController? _motion;
    private GlassMaterialController? _materials;
    private EditModeSession? _editMode;
    private WindowsApplicationCatalog? _applications;
    private ShellRuntime? _shell;
    private ControlCenterWindow? _controlCenter;
    private OnboardingWindow? _onboarding;
    private LocalDiagnosticLog? _diagnostics;
    private SerialDiagnosticWriter? _diagnosticWriter;
    private bool _started;
    private bool _safeMode;
    private bool _systemActive = true;
    private string _lastRecoveryReason = "None";
    private bool _disposed;

    public event Action? ShutdownCompleted;

    public ApplicationRuntime(
        StartupActivation activation,
        Func<bool, Task> restartApplication)
    {
        _activation = activation;
        _restartApplication = restartApplication;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Glass must initialize its runtime on the UI thread.");
        var packagedRoot = PackageIdentityService.TryGetLocalDataPath();
        _dataPaths = packagedRoot is null
            ? GlassDataPaths.CreateDefault()
            : GlassDataPaths.CreatePackaged(packagedRoot);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;

        _dataPaths.EnsureCreated();
        _diagnostics = new LocalDiagnosticLog(_dataPaths.LogDirectory);
        _diagnosticWriter = new SerialDiagnosticWriter(_diagnostics);
        var stateStore = new AtomicJsonStateStore(_dataPaths);
        _sessionHealth = new SessionHealthStore(stateStore);
        var sessionStart = await _sessionHealth.BeginAsync(
            _activation.Mode == StartupActivationMode.SafeMode, cancellationToken);
        _safeMode = sessionStart.EnterSafeMode;
        _lastRecoveryReason = sessionStart.Reason ?? "None";
        _startupTask = new PackagedStartupTaskService();
        var layoutStore = new JsonShellLayoutStore(stateStore, _diagnostics);
        _settings = new AppearanceSettingsRuntime(
            new JsonGlassSettingsStore(stateStore, _diagnostics));
        await _settings.InitializeAsync(cancellationToken);

        _displays = new WindowsDisplayService(message =>
            _diagnosticWriter?.Enqueue("display", message));
        _runningWindows = new RunningWindowTracker();
        _icons = new ShellIconService();
        _interactionPreferences = new WindowsInteractionPreferences();
        _renderingEnvironment = new WindowsRenderingEnvironment();
        _fullscreen = new FullscreenWindowMonitor();
        _renderingPolicy = RenderingPolicy.Resolve(
            _safeMode ? RenderingQualityPreference.Solid :
                _settings.Current.Behavior.RenderingQuality,
            _renderingEnvironment.Current);
        _motion = new GlassMotionController(
            _settings.Current.Appearance.MotionPreference,
            _interactionPreferences.AnimationsEnabled)
        {
            RuntimeAllowsFullMotion = _renderingPolicy.FullMotion,
        };
        _materials = new GlassMaterialController();
        var launcher = new ApplicationLaunchService();
        _applications = new WindowsApplicationCatalog();

        _metrics = new SystemMetricsProvider(samplingInterval: () =>
            _renderingEnvironment?.Current.EnergySaver == true
                ? TimeSpan.FromSeconds(2)
                : TimeSpan.FromSeconds(1));
        _power = new PowerStatusProvider();
        _audio = new AudioEndpointService();
        _clipboard = new ClipboardService();
        _media = new SystemMediaSessionService();
        _weather = new LazyWeatherProvider(stateStore);
        _providerCoordinator = new ProviderCoordinator();
        _providerCoordinator.Register(WidgetProviderAdapters.Metrics(_metrics));
        _providerCoordinator.Register(WidgetProviderAdapters.Power(_power));
        _providerCoordinator.Register(WidgetProviderAdapters.Audio(_audio));
        _providerCoordinator.Register(WidgetProviderAdapters.Media(_media));
        _providerCoordinator.Register(WidgetProviderAdapters.Clipboard(_clipboard));
        _providerCoordinator.Register(WidgetProviderAdapters.Passive("weather"));

        var widgetState = new WidgetStateStore(stateStore);
        var widgetRegistry = new WidgetRegistry();
        BuiltInWidgetRegistration.RegisterAll(widgetRegistry, _providerCoordinator, widgetState);
        _widgetRuntime = new WidgetRuntime(widgetRegistry);
        var widgetViews = new BuiltInWidgetViewFactory(new WidgetViewServices(
            _media, _metrics, _power, _audio, _clipboard, launcher, _icons, _applications, _weather,
            new OneShotLocationService(), () => _settings.Current.Appearance));
        _editMode = new EditModeSession();

        ShellRuntime? shellReference = null;
        var surfaceServices = new ProductSurfaceServices(
            () => shellReference ?? throw new InvalidOperationException("Shell is not ready."),
            () => _settings.Current,
            _widgetRuntime,
            widgetViews,
            _icons,
            _applications,
            _materials,
            _motion,
            () => _renderingPolicy,
            _safeMode,
            _editMode,
            PresentControlCenter,
            () => _widgetSurfaces?.Sync(_shell!.Layout));
        _shell = shellReference = new ShellRuntime(
            layoutStore,
            _displays,
            new BarSurfaceFactory(_displays, _runningWindows, launcher, surfaceServices),
            stateStore);
        _shell.RuntimeFaulted += OnRuntimeFaulted;
        _shell.ShellRecovered += OnShellRecovered;
        _shell.SystemActivityChanged += OnSystemActivityChanged;
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
        if (!_safeMode) _widgetSurfaces.Sync(_shell.Layout);
        _shell.LayoutChanged += OnLayoutChanged;
        _settings.Changed += OnSettingsChanged;
        _interactionPreferences.Changed += OnInteractionPreferencesChanged;
        _renderingEnvironment.Changed += OnRenderingEnvironmentChanged;
        _runningWindows.Start();
        _fullscreen.Changed += OnFullscreenChanged;
        _fullscreen.Start();
        _started = true;
        _startupClock.Stop();
        _diagnosticWriter.Enqueue("startup",
            $"completedMs={_startupClock.ElapsedMilliseconds}; mode={_activation.Mode}; safe={_safeMode}");

        if (!_settings.Current.Behavior.OnboardingCompleted && !_safeMode &&
            _activation.Mode != StartupActivationMode.WindowsStartup)
        {
            _onboarding = new OnboardingWindow(
                _settings, _shell, _materials, _startupTask,
                () => _renderingPolicy, CompleteOnboardingAsync);
            _onboarding.Activate();
        }
        else if (_activation.OpenControlCenter || _safeMode)
        {
            EnsureControlCenter().Present();
        }
    }

    public void Activate()
    {
        _dispatcherQueue.TryEnqueue(PresentControlCenter);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_shell is not null)
        {
            _shell.LayoutChanged -= OnLayoutChanged;
            _shell.RuntimeFaulted -= OnRuntimeFaulted;
            _shell.ShellRecovered -= OnShellRecovered;
            _shell.SystemActivityChanged -= OnSystemActivityChanged;
        }
        if (_settings is not null) _settings.Changed -= OnSettingsChanged;
        if (_interactionPreferences is not null)
            _interactionPreferences.Changed -= OnInteractionPreferencesChanged;
        if (_renderingEnvironment is not null)
            _renderingEnvironment.Changed -= OnRenderingEnvironmentChanged;
        if (_fullscreen is not null) _fullscreen.Changed -= OnFullscreenChanged;

        _onboarding?.Close();
        _onboarding = null;

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
        _clipboard?.Dispose();
        _clipboard = null;
        _media?.Dispose();
        _media = null;
        _weather?.Dispose();
        _weather = null;
        if (_settings is not null) await _settings.DisposeAsync();
        _settings = null;
        _interactionPreferences?.Dispose();
        _interactionPreferences = null;
        _renderingEnvironment?.Dispose();
        _renderingEnvironment = null;
        _fullscreen?.Dispose();
        _fullscreen = null;
        _displays?.Dispose();
        _displays = null;
        _runningWindows?.Dispose();
        _runningWindows = null;
        _icons?.Dispose();
        _icons = null;
        _controlCenter = null;
        _onboarding = null;
        _materials = null;
        _editMode = null;
        _applications = null;
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
        if (_started && _sessionHealth is not null)
            await _sessionHealth.CompleteAsync();
        _sessionHealth = null;
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
        if (_renderingEnvironment is not null)
            _renderingPolicy = RenderingPolicy.Resolve(
                _safeMode ? RenderingQualityPreference.Solid :
                    _settings.Current.Behavior.RenderingQuality,
                _renderingEnvironment.Current);
        _motion.Preference = _settings.Current.Appearance.MotionPreference;
        _motion.RuntimeAllowsFullMotion = _renderingPolicy.FullMotion;
        foreach (var surface in _shell.Surfaces)
            surface.Apply(surface.Definition);
        if (!_safeMode) _widgetSurfaces?.Sync(_shell.Layout);
    }

    private void OnInteractionPreferencesChanged(object? sender, EventArgs args)
    {
        if (_interactionPreferences is null || _motion is null) return;
        _motion.SystemAnimationsEnabled = _interactionPreferences.AnimationsEnabled;
        OnSettingsChanged(sender, args);
    }

    private void OnRenderingEnvironmentChanged(object? sender, EventArgs args)
    {
        if (_renderingEnvironment is null || _settings is null) return;
        _renderingPolicy = RenderingPolicy.Resolve(
            _safeMode ? RenderingQualityPreference.Solid :
                _settings.Current.Behavior.RenderingQuality,
            _renderingEnvironment.Current);
        OnSettingsChanged(sender, args);
    }

    private void OnFullscreenChanged(FullscreenWindowSnapshot snapshot)
    {
        if (_settings is null || _shell is null) return;
        var respect = _settings.Current.Taskbar.RespectFullscreenApplications;
        _shell.ApplyFullscreenSuppression(snapshot, respect);
        _widgetSurfaces?.ApplyFullscreenSuppression(snapshot, respect);
    }

    private void OnShellRecovered(object? sender, EventArgs args)
    {
        _lastRecoveryReason = "Explorer taskbar recreated";
        _runningWindows?.Refresh();
        try { _applications?.Refresh(); }
        catch (Exception exception) { OnRuntimeFaulted(exception); }
        _diagnosticWriter?.Enqueue("recovery", "Explorer taskbar recreated; surfaces reconciled");
    }

    private async void OnSystemActivityChanged(SystemActivityState state)
    {
        if (_systemActive == state.IsActive || _widgetRuntime is null) return;
        _systemActive = state.IsActive;
        _lastRecoveryReason = state.Reason;
        try
        {
            if (!state.IsActive)
            {
                foreach (var widget in _widgetRuntime.Instances)
                    await _widgetRuntime.SetVisibleAsync(widget.Configuration.InstanceId, false);
            }
            else
            {
                _displays?.Refresh();
                if (_shell is not null)
                    foreach (var surface in _shell.Surfaces)
                        await surface.ReconcileDisplayAsync();
                foreach (var surface in _shell?.Surfaces ?? [])
                    surface.Apply(surface.Definition);
                if (!_safeMode && _shell is not null) _widgetSurfaces?.Sync(_shell.Layout);
                _runningWindows?.Refresh();
            }
            _diagnosticWriter?.Enqueue("recovery", $"system={state.Reason}; active={state.IsActive}");
        }
        catch (Exception exception) { OnRuntimeFaulted(exception); }
    }

    private ControlCenterWindow EnsureControlCenter()
    {
        if (_controlCenter is not null) return _controlCenter;
        if (_shell is null || _displays is null || _applications is null ||
            _settings is null || _editMode is null || _materials is null)
            throw new InvalidOperationException("Glass is not ready to open Control Center.");
        _controlCenter = new ControlCenterWindow(
            _shell, _displays, _applications, BuiltInWidgetCatalog.All,
            _settings, _editMode, _materials, () => _renderingPolicy,
            () => { if (!_safeMode) _widgetSurfaces?.Sync(_shell.Layout); },
            ShutdownAsync,
            _startupTask ?? new PackagedStartupTaskService(),
            CreateDiagnosticsSnapshot,
            _restartApplication,
            ResetConfigurationAsync,
            _safeMode);
        return _controlCenter;
    }

    private void PresentControlCenter() => EnsureControlCenter().Present();

    private ValueTask CompleteOnboardingAsync()
    {
        _onboarding = null;
        PresentControlCenter();
        return ValueTask.CompletedTask;
    }

    private string CreateDiagnosticsSnapshot()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return string.Join(Environment.NewLine,
        [
            $"Glass version: {Glass.Core.Product.ProductVersion.Current.Informational}",
            $"Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}",
            $"OS: {Environment.OSVersion.Version}",
            $"Package mode: {(PackageIdentityService.IsPackaged ? "Packaged" : "Unpackaged")}",
            $"Startup duration: {_startupClock.ElapsedMilliseconds} ms",
            $"Working set: {process.WorkingSet64 / (1024 * 1024)} MB",
            $"Private memory: {process.PrivateMemorySize64 / (1024 * 1024)} MB",
            $"Active bars: {_shell?.Surfaces.Count ?? 0}",
            $"Configured widgets: {_shell?.Layout.WidgetInstances.Count ?? 0}",
            $"Active providers: {_providerCoordinator?.ActiveProviderCount ?? 0}",
            $"Rendering quality: {_renderingPolicy.Quality}",
            $"Animation mode: {_settings?.Current.Appearance.MotionPreference}",
            $"Display count: {_displays?.Displays.Count ?? 0}",
            $"Safe mode: {_safeMode}",
            $"Last recovery reason: {_lastRecoveryReason}",
            "Personal widget content and precise locations are excluded.",
        ]);
    }

    private async Task ResetConfigurationAsync()
    {
        if (_shell is null || _settings is null) return;
        var recovery = new ConfigurationRecoveryService(_dataPaths);
        var result = recovery.ResetConfiguration(includeWidgetContents: false);
        _diagnosticWriter?.Enqueue("recovery",
            $"configuration reset; files={result.RemovedFiles}; backup created");
        await _shell.ResetAsync();
        await _settings.UpdateAsync(_ => new GlassSettings
        {
            Behavior = new ProductBehaviorSettings { OnboardingCompleted = true },
        });
        await _restartApplication(true);
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
