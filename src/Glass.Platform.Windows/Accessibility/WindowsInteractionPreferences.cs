using Windows.UI.ViewManagement;

namespace Glass.Platform.Windows.Accessibility;

public sealed class WindowsInteractionPreferences : IDisposable
{
    private readonly UISettings _settings = new();
    private readonly AccessibilitySettings _accessibility = new();

    public bool AnimationsEnabled => _settings.AnimationsEnabled;
    public bool HighContrast => _accessibility.HighContrast;
    public event EventHandler? Changed;

    public WindowsInteractionPreferences()
    {
        // ColorValuesChanged and HighContrastChanged are unsupported in desktop apps.
        _settings.AdvancedEffectsEnabledChanged += OnAdvancedEffectsChanged;
    }

    public void Dispose()
    {
        _settings.AdvancedEffectsEnabledChanged -= OnAdvancedEffectsChanged;
        Changed = null;
    }

    private void OnAdvancedEffectsChanged(UISettings sender, object args) =>
        Changed?.Invoke(this, EventArgs.Empty);
}
