using Glass.Infrastructure.Diagnostics;
using Glass.Infrastructure.Persistence;
using Glass.Infrastructure.Storage;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Applications;
using Glass.Shell.Runtime;
using Glass.Widgets.BuiltIn;
using Glass.Widgets.Runtime;
using Glass.Widgets.Abstractions;

namespace Glass.App.Runtime;

internal sealed class ApplicationRuntime : IAsyncDisposable
{
    private readonly GlassDataPaths _dataPaths = GlassDataPaths.CreateDefault();
    private WindowsDisplayService? _displays;
    private RunningWindowTracker? _runningWindows;
    private ShellIconService? _icons;
    private WidgetSurfaceManager? _widgetSurfaces;
    private WidgetRuntime? _widgetRuntime;
    private ShellRuntime? _shell;
    private DevelopmentShellControlsWindow? _developmentWindow;
    private LocalDiagnosticLog? _diagnostics;
    private SerialDiagnosticWriter? _diagnosticWriter;
    private bool _started;
    private bool _disposed;

    public event Action? ShutdownCompleted;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _dataPaths.EnsureCreated();
        _diagnostics = new LocalDiagnosticLog(_dataPaths.LogDirectory);
        _diagnosticWriter = new SerialDiagnosticWriter(_diagnostics);
        var stateStore = new AtomicJsonStateStore(_dataPaths);
        var layoutStore = new JsonShellLayoutStore(stateStore, _diagnostics);
        _displays = new WindowsDisplayService();
        _runningWindows = new RunningWindowTracker();
        _icons = new ShellIconService();
        var launcher = new ApplicationLaunchService();
        _shell = new ShellRuntime(
            layoutStore,
            _displays,
            new BarSurfaceFactory(_displays, _runningWindows, launcher),
            stateStore);
        _shell.RuntimeFaulted += OnRuntimeFaulted;
        await _shell.InitializeAsync(cancellationToken);
        _runningWindows.Start();
        var widgetRegistry = new WidgetRegistry();
        BuiltInWidgetRegistration.RegisterAll(widgetRegistry);
        _widgetRuntime = new WidgetRuntime(widgetRegistry);
        foreach (var widget in _shell.Layout.WidgetInstances)
        {
            var instance = await _widgetRuntime.CreateAsync(
                new WidgetInstanceConfiguration(
                    new WidgetInstanceId(widget.WidgetInstanceId),
                    new WidgetTypeId(widget.WidgetTypeId),
                    new WidgetSize(widget.Size.Width, widget.Size.Height),
                    widget.Configuration),
                cancellationToken);
            await instance.SetVisibleAsync(true, cancellationToken);
        }
        _widgetSurfaces = new WidgetSurfaceManager(
            _displays,
            definition => _shell.UpdateStandaloneWidgetAsync(definition).AsTask());
        _widgetSurfaces.Sync(_shell.Layout);
        IReadOnlyList<Glass.Core.Applications.ApplicationDescriptor> applications;
        try { applications = new WindowsApplicationCatalog().Enumerate(); }
        catch (Exception exception)
        {
            applications = [];
            _diagnosticWriter.Enqueue("applications", exception.ToString());
        }

        _developmentWindow = new DevelopmentShellControlsWindow(
            _shell,
            _displays,
            applications,
            BuiltInWidgetCatalog.All,
            () => _widgetSurfaces.Sync(_shell.Layout),
            ShutdownAsync);
        _developmentWindow.Present();
        _started = true;
    }

    public void Activate()
    {
        if (_developmentWindow is { } window)
        {
            window.DispatcherQueue.TryEnqueue(window.Present);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_shell is not null)
        {
            await _shell.DisposeAsync();
            _shell.RuntimeFaulted -= OnRuntimeFaulted;
            _shell = null;
        }

        _widgetSurfaces?.Dispose();
        _widgetSurfaces = null;
        if (_widgetRuntime is not null)
        {
            await _widgetRuntime.DisposeAsync();
            _widgetRuntime = null;
        }
        _displays?.Dispose();
        _displays = null;
        _runningWindows?.Dispose();
        _runningWindows = null;
        _icons?.Dispose();
        _icons = null;
        _developmentWindow = null;
        if (_diagnosticWriter is not null)
        {
            await _diagnosticWriter.FlushAsync();
            _diagnosticWriter = null;
        }
        _diagnostics?.Dispose();
        _diagnostics = null;
        ShutdownCompleted = null;
    }

    private async Task ShutdownAsync()
    {
        var handler = ShutdownCompleted;
        await DisposeAsync();
        handler?.Invoke();
    }

    private void OnRuntimeFaulted(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine($"Shell runtime failure: {exception}");
        if (_diagnosticWriter is { } writer)
        {
            writer.Enqueue("runtime", exception.ToString());
        }
    }
}
