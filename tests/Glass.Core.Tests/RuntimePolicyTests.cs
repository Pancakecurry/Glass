using Glass.Core.Appearance;
using Glass.Core.Geometry;
using Glass.Core.Runtime;
using Xunit;

namespace Glass.Core.Tests;

public sealed class RuntimePolicyTests
{
    [Theory]
    [InlineData(null, StartupActivationMode.Normal, true, false)]
    [InlineData("--windows-startup", StartupActivationMode.WindowsStartup, false, false)]
    [InlineData("--safe-mode", StartupActivationMode.SafeMode, true, true)]
    public void StartupActivation_MapsLaunchIntent(
        string? argument,
        StartupActivationMode mode,
        bool opensControlCenter,
        bool forcesSafeRendering)
    {
        var activation = StartupActivation.Parse(argument is null ? [] : [argument]);

        Assert.Equal(mode, activation.Mode);
        Assert.Equal(opensControlCenter, activation.OpenControlCenter);
        Assert.Equal(forcesSafeRendering, activation.ForceSafeRendering);
    }

    [Fact]
    public void HighContrast_AlwaysSelectsSolidRendering()
    {
        var environment = new RenderingEnvironment(
            HighContrast: true,
            TransparencyEnabled: true,
            AnimationsEnabled: true,
            EnergySaver: false,
            RemoteSession: false,
            EffectsSupported: true);

        var policy = RenderingPolicy.Resolve(RenderingQualityPreference.Full, environment);

        Assert.Equal(RenderingQualityLevel.Solid, policy.Quality);
        Assert.False(policy.Transparency);
        Assert.False(policy.FullMotion);
    }

    [Fact]
    public void UnsupportedEffects_CannotBeForcedByUserPreference()
    {
        var environment = new RenderingEnvironment(
            false, true, true, false, false, EffectsSupported: false);

        Assert.Equal(RenderingQualityLevel.Solid,
            RenderingPolicy.Resolve(RenderingQualityPreference.Full, environment).Quality);
    }

    [Theory]
    [InlineData(true, false, RenderingQualityLevel.Reduced)]
    [InlineData(false, true, RenderingQualityLevel.Reduced)]
    [InlineData(false, false, RenderingQualityLevel.Balanced)]
    public void AutoRendering_AdaptsToConstrainedEnvironments(
        bool energySaver,
        bool remoteSession,
        RenderingQualityLevel expected)
    {
        var environment = new RenderingEnvironment(
            false, true, true, energySaver, remoteSession, true);

        Assert.Equal(expected,
            RenderingPolicy.Resolve(RenderingQualityPreference.Auto, environment).Quality);
    }

    [Fact]
    public void FullscreenPolicy_DistinguishesFullscreenFromWorkAreaMaximized()
    {
        var display = new LogicalRect(-1920, 0, 1920, 1080);
        var fullscreen = new ForegroundWindowGeometry(
            display, display, false, true, false, false);
        var maximized = fullscreen with
        {
            WindowBounds = new LogicalRect(-1920, 0, 1920, 1040),
        };

        Assert.True(FullscreenPolicy.IsFullscreen(fullscreen));
        Assert.False(FullscreenPolicy.IsFullscreen(maximized));
        Assert.False(FullscreenPolicy.IsFullscreen(fullscreen with { IsGlassWindow = true }));
    }
}
