using Glass.App.Runtime;
using Glass.Core.Runtime;
using Microsoft.Windows.AppLifecycle;
using Microsoft.UI.Xaml;

namespace Glass.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WinUI owns the Application lifetime; the runtime is disposed by the async shutdown path.")]
public partial class App : Application
{
    private const string InstanceKey = "Glass.Primary";
    private AppInstance? _registeredInstance;
    private ApplicationRuntime? _runtime;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await LaunchAsync(args);
        }
        catch (Exception exception)
        {
            var logPath = StartupFailureReporter.TryWrite(exception);
            StartupFailureReporter.TryShow(logPath);
            try
            {
                await ShutdownAsync();
            }
            catch (Exception shutdownException)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Glass shutdown after startup failure also failed: {shutdownException}");
                Exit();
            }
        }
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        var current = AppInstance.GetCurrent();
        var registered = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!registered.IsCurrent)
        {
            await registered.RedirectActivationToAsync(current.GetActivatedEventArgs());
            Exit();
            return;
        }

        _registeredInstance = registered;
        _registeredInstance.Activated += OnRedirectedActivation;
        var arguments = args.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (current.GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask)
            arguments.Add("--windows-startup");
        _runtime = new ApplicationRuntime(
            StartupActivation.Parse(arguments),
            RestartAsync);
        _runtime.ShutdownCompleted += OnShutdownCompleted;
        await _runtime.StartAsync();
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments args) =>
        _runtime?.Activate();

    private async void OnShutdownCompleted() => await ShutdownAsync();

    private async Task RestartAsync(bool safeMode)
    {
        var executable = Environment.ProcessPath ??
            throw new InvalidOperationException("Glass could not resolve its executable path.");
        await ReleaseRuntimeAsync();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = executable,
            Arguments = safeMode ? "--safe-mode" : string.Empty,
            UseShellExecute = true,
        });
        Exit();
    }

    private async Task ShutdownAsync()
    {
        await ReleaseRuntimeAsync();
        Exit();
    }

    private async Task ReleaseRuntimeAsync()
    {
        if (_registeredInstance is not null)
        {
            _registeredInstance.Activated -= OnRedirectedActivation;
            _registeredInstance.UnregisterKey();
            _registeredInstance = null;
        }

        if (_runtime is not null)
        {
            _runtime.ShutdownCompleted -= OnShutdownCompleted;
            await _runtime.DisposeAsync();
            _runtime = null;
        }
    }
}
