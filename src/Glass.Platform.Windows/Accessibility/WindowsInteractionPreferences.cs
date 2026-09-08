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
        _settings.ColorValuesChanged += OnColorValuesChanged;
        _accessibility.HighContrastChanged += OnHighContrastChanged;
    }

    public void Dispose()
    {
        _settings.ColorValuesChanged -= OnColorValuesChanged;
        _accessibility.HighContrastChanged -= OnHighContrastChanged;
        Changed = null;
    }

    private void OnColorValuesChanged(UISettings sender, object args) =>
        Changed?.Invoke(this, EventArgs.Empty);

    private void OnHighContrastChanged(AccessibilitySettings sender, object args) =>
        Changed?.Invoke(this, EventArgs.Empty);
}
