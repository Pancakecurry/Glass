using Glass.Core.Appearance;

namespace Glass.Core.Runtime;

public enum RenderingQualityLevel { Full, Balanced, Reduced, Solid }

public sealed record RenderingEnvironment(
    bool HighContrast,
    bool TransparencyEnabled,
    bool AnimationsEnabled,
    bool EnergySaver,
    bool RemoteSession,
    bool EffectsSupported);

public sealed record RenderingPolicy(
    RenderingQualityLevel Quality,
    bool Transparency,
    bool Shadows,
    bool Magnification,
    bool FullMotion)
{
    public static RenderingPolicy Resolve(
        RenderingQualityPreference preference,
        RenderingEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.HighContrast)
            return For(RenderingQualityLevel.Solid, environment);
        if (!environment.EffectsSupported || !environment.TransparencyEnabled)
            return For(RenderingQualityLevel.Solid, environment);

        var quality = preference switch
        {
            RenderingQualityPreference.Full => RenderingQualityLevel.Full,
            RenderingQualityPreference.Balanced => RenderingQualityLevel.Balanced,
            RenderingQualityPreference.Reduced => RenderingQualityLevel.Reduced,
            RenderingQualityPreference.Solid => RenderingQualityLevel.Solid,
            _ when environment.EnergySaver || environment.RemoteSession ||
                !environment.AnimationsEnabled => RenderingQualityLevel.Reduced,
            _ => RenderingQualityLevel.Balanced,
        };
        return For(quality, environment);
    }

    private static RenderingPolicy For(
        RenderingQualityLevel quality,
        RenderingEnvironment environment) => quality switch
        {
            RenderingQualityLevel.Full => new(quality, true, true,
                environment.AnimationsEnabled, environment.AnimationsEnabled),
            RenderingQualityLevel.Balanced => new(quality, true, true,
                environment.AnimationsEnabled, environment.AnimationsEnabled),
            RenderingQualityLevel.Reduced => new(quality,
                environment.TransparencyEnabled && environment.EffectsSupported,
                false, false, false),
            _ => new(quality, false, false, false, false),
        };
}
