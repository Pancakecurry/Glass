using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;

namespace Glass.Platform.Windows.Accessibility;

public sealed class WindowsInteractionPreferences : IDisposable
{
    private readonly UISettings _settings = new();
    private readonly AccessibilitySettings _accessibility = new();
    private readonly bool _animationsChangedAvailable;

    public bool AnimationsEnabled => _settings.AnimationsEnabled;
    public bool HighContrast => _accessibility.HighContrast;
    public event EventHandler? Changed;

    public WindowsInteractionPreferences()
    {
        // ColorValuesChanged and HighContrastChanged are unsupported in desktop apps.
        _animationsChangedAvailable = ApiInformation.IsEventPresent(
            "Windows.UI.ViewManagement.UISettings", "AnimationsEnabledChanged");
        if (_animationsChangedAvailable)
            _settings.AnimationsEnabledChanged += OnAnimationsChanged;
    }

    public void Dispose()
    {
        if (_animationsChangedAvailable)
            _settings.AnimationsEnabledChanged -= OnAnimationsChanged;
        Changed = null;
    }

    private void OnAnimationsChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args) =>
        Changed?.Invoke(this, EventArgs.Empty);
}
