using Glass.Core.Appearance;

namespace Glass.Core.Tests;

public sealed class AppearanceSettingsTests
{
    [Fact]
    public void Normalize_ClampsUnsafeMaterialAndInteractionValues()
    {
        var settings = new GlobalAppearanceSettings
        {
            Material = MaterialSettings.GlassClear with
            {
                TintColor = "not-a-color",
                MaterialIntensity = 4,
                CornerRadius = double.NaN,
                OverallOpacity = 0.1,
            },
            ApplicationIconSize = 200,
            MagnificationMaximumScale = 9,
        }.Normalize();

        Assert.Equal("#F4F7FA", settings.Material.TintColor);
        Assert.Equal(1, settings.Material.MaterialIntensity);
        Assert.Equal(20, settings.Material.CornerRadius);
        Assert.Equal(0.45, settings.Material.OverallOpacity);
        Assert.Equal(48, settings.ApplicationIconSize);
        Assert.Equal(1.35, settings.MagnificationMaximumScale);
    }

    [Fact]
    public void Resolver_AppliesSparseSurfaceOverrideOverGlobalMaterial()
    {
        var id = Guid.NewGuid();
        var settings = new GlassSettings
        {
            Appearance = new GlobalAppearanceSettings
            {
                Material = MaterialSettings.GlassClear with { CornerRadius = 18 },
            },
            BarAppearanceOverrides = new Dictionary<Guid, MaterialOverride>
            {
                [id] = new() { ShadowStrength = 0.8 },
            },
        };

        var resolved = AppearanceResolver.ForBar(settings, id);

        Assert.Equal(18, resolved.CornerRadius);
        Assert.Equal(0.8, resolved.ShadowStrength);
        Assert.Equal(MaterialPreset.Clear, resolved.Preset);
    }

    [Fact]
    public void ReducedMotion_DisablesSpringAndMagnification()
    {
        var motion = MotionPolicy.Resolve(
            MotionIntent.Snap, MotionPreference.Reduced, systemAnimationsEnabled: true);

        Assert.True(motion.IsImmediate);
        Assert.False(motion.UsesSpring);
        Assert.Equal(1, MotionPolicy.MagnificationScale(
            MagnificationMode.Expressive, 1.35, systemAnimationsEnabled: false));
    }
}
