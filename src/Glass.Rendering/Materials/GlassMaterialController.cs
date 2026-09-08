using Glass.Core.Appearance;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Glass.Rendering.Materials;

public enum GlassSurfaceRole { Bar, Widget, ControlCenter, Flyout }

public sealed class GlassMaterialController
{
    private readonly AccessibilitySettings _accessibility = new();

    public void Apply(
        Window window,
        Border surface,
        MaterialSettings settings,
        ThemeMode themeMode,
        GlassSurfaceRole role)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(surface);
        settings = settings.Normalize();
        surface.RequestedTheme = themeMode switch
        {
            ThemeMode.Light => ElementTheme.Light,
            ThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        var solid = _accessibility.HighContrast || settings.Preset == MaterialPreset.Solid;
        try
        {
            window.SystemBackdrop = solid
                ? null
                : role == GlassSurfaceRole.ControlCenter
                    ? new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt }
                    : new DesktopAcrylicBackdrop();
        }
        catch
        {
            solid = true;
            window.SystemBackdrop = null;
        }

        var tint = ParseColor(settings.TintColor);
        var overlayAlpha = solid
            ? byte.MaxValue
            : Alpha(Math.Clamp(
                settings.TintStrength + ((1 - settings.MaterialIntensity) * 0.34), 0.04, 0.72));
        surface.Background = new SolidColorBrush(Color.FromArgb(
            overlayAlpha, tint.R, tint.G, tint.B));
        surface.BorderBrush = new SolidColorBrush(Color.FromArgb(
            Alpha(settings.BorderStrength * 0.74), 255, 255, 255));
        surface.BorderThickness = new Thickness(settings.BorderStrength <= 0 ? 0 : 1);
        surface.CornerRadius = new CornerRadius(settings.CornerRadius);
        surface.Opacity = settings.OverallOpacity;
        surface.Shadow = settings.ShadowStrength > 0.02 ? new ThemeShadow() : null;
        surface.Translation = settings.ShadowStrength > 0.02
            ? new System.Numerics.Vector3(0, 0, (float)(12 + settings.ShadowStrength * 20))
            : System.Numerics.Vector3.Zero;
    }

    public static SolidColorBrush EdgeHighlight(MaterialSettings settings)
    {
        var strength = settings.Normalize().EdgeHighlightStrength;
        return new SolidColorBrush(Color.FromArgb(Alpha(strength * 0.72), 255, 255, 255));
    }

    private static byte Alpha(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0, 1) * byte.MaxValue);

    private static Color ParseColor(string value)
    {
        var normalized = value.TrimStart('#');
        if (normalized.Length == 8) normalized = normalized[2..];
        return Color.FromArgb(255,
            Convert.ToByte(normalized[0..2], 16),
            Convert.ToByte(normalized[2..4], 16),
            Convert.ToByte(normalized[4..6], 16));
    }
}
