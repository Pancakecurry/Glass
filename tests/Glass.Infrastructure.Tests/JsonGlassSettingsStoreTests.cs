using Glass.Core.Appearance;
using Glass.Infrastructure.Persistence;
using Glass.Infrastructure.Storage;
using Xunit;

namespace Glass.Infrastructure.Tests;

public sealed class JsonGlassSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "glass-settings-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveLoad_RoundTripsVersionedSettingsAndPresets()
    {
        var paths = new GlassDataPaths(_root);
        paths.EnsureCreated();
        var store = new JsonGlassSettingsStore(new AtomicJsonStateStore(paths));
        var custom = new AppearancePresetDefinition(
            Guid.NewGuid(), "Quiet blue",
            MaterialSettings.GlassClear with { TintColor = "#336699" }, false);
        var expected = new GlassSettings
        {
            Appearance = new GlobalAppearanceSettings { ThemeMode = ThemeMode.Dark },
            CustomPresets = [custom],
        };

        await store.SaveAsync(expected, TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(new GlassSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(ThemeMode.Dark, loaded.Appearance.ThemeMode);
        Assert.Single(loaded.CustomPresets);
        Assert.Equal("#336699", loaded.CustomPresets[0].Material.TintColor);
    }

    [Fact]
    public async Task SchemaZero_MigratesWithSafeCollectionDefaults()
    {
        var paths = new GlassDataPaths(_root);
        paths.EnsureCreated();
        var state = new AtomicJsonStateStore(paths);
        await state.WriteAsync("settings",
            """{"schemaVersion":0,"payload":{"appearance":{"themeMode":"light"}}}""",
            TestContext.Current.CancellationToken);

        var loaded = await new JsonGlassSettingsStore(state)
            .LoadAsync(new GlassSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(ThemeMode.Light, loaded.Appearance.ThemeMode);
        Assert.Empty(loaded.BarAppearanceOverrides);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
