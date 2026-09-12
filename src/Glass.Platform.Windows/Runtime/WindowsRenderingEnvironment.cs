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
    private readonly bool _animationsChangedAvailable;
    private bool _disposed;

    public WindowsRenderingEnvironment()
    {
        // These supported events retain live effects/motion updates without the
        // desktop-unsupported ColorValuesChanged and HighContrastChanged events.
        _ui.AdvancedEffectsEnabledChanged += OnChanged;
        _animationsChangedAvailable = ApiInformation.IsEventPresent(
            "Windows.UI.ViewManagement.UISettings", "AnimationsEnabledChanged");
        if (_animationsChangedAvailable)
            _ui.AnimationsEnabledChanged += OnAnimationsChanged;
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
        if (_animationsChangedAvailable)
            _ui.AnimationsEnabledChanged -= OnAnimationsChanged;
        PowerManager.EnergySaverStatusChanged -= OnEnergySaverChanged;
        Changed = null;
    }

    private void OnChanged(object sender, object args) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnAnimationsChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args) =>
        Changed?.Invoke(this, EventArgs.Empty);
    private void OnEnergySaverChanged(object? sender, object args) => Changed?.Invoke(this, EventArgs.Empty);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
