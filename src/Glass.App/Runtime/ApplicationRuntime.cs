using Glass.Infrastructure.Diagnostics;
using Glass.Infrastructure.Persistence;
using Glass.Infrastructure.Storage;
using Glass.Platform.Windows.Displays;
using Glass.Shell.Runtime;

namespace Glass.App.Runtime;

internal sealed class ApplicationRuntime : IAsyncDisposable
{
    private readonly GlassDataPaths _dataPaths = GlassDataPaths.CreateDefault();
    private readonly List<Task> _diagnosticWrites = [];
    private WindowsDisplayService? _displays;
    private ShellRuntime? _shell;
    private DevelopmentShellControlsWindow? _developmentWindow;
    private LocalDiagnosticLog? _diagnostics;
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
        var stateStore = new AtomicJsonStateStore(_dataPaths);
        var layoutStore = new JsonShellLayoutStore(stateStore, _diagnostics);
        _displays = new WindowsDisplayService();
        _shell = new ShellRuntime(
            layoutStore,
            _displays,
            new BarSurfaceFactory(_displays));
        _shell.RuntimeFaulted += OnRuntimeFaulted;
        await _shell.InitializeAsync(cancellationToken);

        _developmentWindow = new DevelopmentShellControlsWindow(
            _shell,
            _displays,
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

        _displays?.Dispose();
        _displays = null;
        _developmentWindow = null;
        Task[] writes;
        lock (_diagnosticWrites)
        {
            writes = _diagnosticWrites.ToArray();
        }

        await Task.WhenAll(writes);
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
        if (_diagnostics is { } diagnostics)
        {
            var write = WriteDiagnosticAsync(diagnostics, exception);
            lock (_diagnosticWrites)
            {
                _diagnosticWrites.Add(write);
            }
        }
    }

    private static async Task WriteDiagnosticAsync(
        LocalDiagnosticLog diagnostics,
        Exception exception)
    {
        try
        {
            await diagnostics.WriteAsync("runtime", exception.ToString());
        }
        catch (Exception diagnosticException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Local runtime diagnostic failed: {diagnosticException}");
        }
    }
}
