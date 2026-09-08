using Glass.App.Runtime;
using Microsoft.Windows.AppLifecycle;
using Microsoft.UI.Xaml;

namespace Glass.App;

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
        _runtime = new ApplicationRuntime();
        _runtime.ShutdownCompleted += OnShutdownCompleted;
        try
        {
            await _runtime.StartAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Glass startup failed: {exception}");
            await ShutdownAsync();
        }
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments args) =>
        _runtime?.Activate();

    private async void OnShutdownCompleted() => await ShutdownAsync();

    private async Task ShutdownAsync()
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

        Exit();
    }
}
