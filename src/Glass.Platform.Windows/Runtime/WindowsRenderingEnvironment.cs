using System.Runtime.InteropServices;
using Glass.Core.Runtime;
using Windows.Foundation.Metadata;
using Windows.System.Power;
using Windows.UI.ViewManagement;

namespace Glass.Platform.Windows.Runtime;

public sealed partial class WindowsRenderingEnvironment : IDisposable
{
    private const int SmRemoteSession = 0x1000;
    private readonly UISettings _ui = new();
    private readonly AccessibilitySettings _accessibility = new();
    private bool _disposed;

    public WindowsRenderingEnvironment()
    {
        // The WinRT color and high-contrast change events are unsupported in desktop apps.
        _ui.AdvancedEffectsEnabledChanged += OnChanged;
        PowerManager.EnergySaverStatusChanged += OnEnergySaverChanged;
    }

    public event EventHandler? Changed;

    public RenderingEnvironment Current => new(
        _accessibility.HighContrast,
        _ui.AdvancedEffectsEnabled,
        _ui.AnimationsEnabled,
        PowerManager.EnergySaverStatus == EnergySaverStatus.On,
        GetSystemMetrics(SmRemoteSession) != 0,
        ApiInformation.IsTypePresent("Windows.UI.Composition.Compositor"));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ui.AdvancedEffectsEnabledChanged -= OnChanged;
        PowerManager.EnergySaverStatusChanged -= OnEnergySaverChanged;
        Changed = null;
    }

    private void OnChanged(object sender, object args) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnEnergySaverChanged(object? sender, object args) => Changed?.Invoke(this, EventArgs.Empty);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
