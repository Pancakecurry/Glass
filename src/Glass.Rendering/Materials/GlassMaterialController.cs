using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using Glass.Core.Appearance;
using Glass.Core.Runtime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Glass.Rendering.Materials;

public enum GlassSurfaceRole { Bar, Widget, ControlCenter, Flyout }

public sealed class GlassMaterialController
{
    [SuppressMessage("Performance", "CA1822", Justification =
        "The controller is the injected material boundary and will own cached resources after runtime validation.")]
    public void Apply(
        Window window,
        Border surface,
        MaterialSettings settings,
        ThemeMode themeMode,
        GlassSurfaceRole role,
        RenderingPolicy? policy = null)
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

        var quality = policy?.Quality ?? RenderingQualityLevel.Balanced;
        var solid = settings.Preset == MaterialPreset.Solid ||
            quality == RenderingQualityLevel.Solid;
        try
        {
            window.SystemBackdrop = solid
                ? null
                : role == GlassSurfaceRole.ControlCenter
                    ? new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt }
                    : new DesktopAcrylicBackdrop();
        }
        catch (Exception exception) when (exception is InvalidOperationException or
            NotSupportedException or COMException)
        {
            System.Diagnostics.Debug.WriteLine($"Glass backdrop unavailable: {exception.Message}");
            solid = true;
            window.SystemBackdrop = null;
        }

        var tint = AdjustLuminosity(ParseColor(settings.TintColor), settings.Luminosity);
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
        var shadows = policy?.Shadows ?? true;
        surface.Shadow = shadows && settings.ShadowStrength > 0.02 ? new ThemeShadow() : null;
        surface.Translation = shadows && settings.ShadowStrength > 0.02
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

    private static Color AdjustLuminosity(Color color, double luminosity)
    {
        var target = luminosity >= 0.5 ? byte.MaxValue : byte.MinValue;
        var amount = Math.Abs(luminosity - 0.5) * 0.72;
        static byte Blend(byte source, byte target, double amount) =>
            (byte)Math.Round(source + ((target - source) * amount));
        return Color.FromArgb(color.A,
            Blend(color.R, target, amount),
            Blend(color.G, target, amount),
            Blend(color.B, target, amount));
    }
}
