using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Infrastructure.Persistence;
using Glass.Infrastructure.Storage;
using Xunit;

namespace Glass.Infrastructure.Tests;

public sealed class JsonShellLayoutStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"glass-layout-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task RoundTripsVersionedPolymorphicLayout()
    {
        var store = CreateStore();
        var expected = ShellLayout.CreateDefault(DisplayTarget.PrimaryFallback);
        var cancellationToken = TestContext.Current.CancellationToken;

        await store.SaveAsync(expected, cancellationToken);
        var actual = await store.LoadAsync(new ShellLayout([]), cancellationToken);

        Assert.Single(actual.Bars);
        Assert.IsType<AnchoredPlacement>(actual.Bars[0].Placement);
        Assert.Equal(expected.Bars[0].Id, actual.Bars[0].Id);
    }

    [Fact]
    public async Task MigratesSchemaOneWithoutChangingBarIdentityOrPlacement()
    {
        var paths = new GlassDataPaths(_root);
        var state = new AtomicJsonStateStore(paths);
        var expected = ShellLayout.CreateDefault(DisplayTarget.PrimaryFallback);
        var current = new JsonShellLayoutStore(state);
        await current.SaveAsync(expected, TestContext.Current.CancellationToken);
        var json = await state.ReadAsync("shell-layout", TestContext.Current.CancellationToken);
        await state.WriteAsync("shell-layout", json!.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1"),
            TestContext.Current.CancellationToken);

        var migrated = await current.LoadAsync(new ShellLayout([]), TestContext.Current.CancellationToken);

        Assert.Equal(expected.Bars[0].Id, migrated.Bars[0].Id);
        Assert.Equal(expected.Bars[0].Placement, migrated.Bars[0].Placement);
        Assert.Single(migrated.Bars[0].Content.OfType<RunningApplicationsSlotBarItem>());
    }

    [Fact]
    public async Task MissingDocumentReturnsDefaultsWithoutBackup()
    {
        var paths = new GlassDataPaths(_root);
        var fallback = ShellLayout.CreateDefault(DisplayTarget.PrimaryFallback);

        var actual = await CreateStore(paths).LoadAsync(
            fallback,
            TestContext.Current.CancellationToken);

        Assert.Same(fallback, actual);
        Assert.Empty(Directory.EnumerateFiles(paths.BackupDirectory));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"schemaVersion\":999,\"payload\":{\"bars\":[]}}")]
    public async Task UnsafeDocumentIsBackedUpAndDefaultsAreReturned(string content)
    {
        var paths = new GlassDataPaths(_root);
        var state = new AtomicJsonStateStore(paths);
        var cancellationToken = TestContext.Current.CancellationToken;
        await state.WriteAsync("shell-layout", content, cancellationToken);
        var fallback = ShellLayout.CreateDefault(DisplayTarget.PrimaryFallback);

        var actual = await new JsonShellLayoutStore(state)
            .LoadAsync(fallback, cancellationToken);

        Assert.Same(fallback, actual);
        Assert.Single(Directory.EnumerateFiles(paths.BackupDirectory));
        Assert.False(File.Exists(state.GetStatePath("shell-layout")));
    }

    private JsonShellLayoutStore CreateStore(GlassDataPaths? paths = null) =>
        new(new AtomicJsonStateStore(paths ?? new GlassDataPaths(_root)));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
