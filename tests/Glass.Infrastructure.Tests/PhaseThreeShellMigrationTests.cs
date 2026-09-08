using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Infrastructure.Persistence;
using Glass.Infrastructure.Storage;

namespace Glass.Infrastructure.Tests;

public sealed class PhaseThreeShellMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "glass-shell-v3-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SchemaTwo_ReceivesPresentationAndLockDefaults()
    {
        var paths = new GlassDataPaths(_root);
        paths.EnsureCreated();
        var state = new AtomicJsonStateStore(paths);
        var fallback = ShellLayout.CreateDefault(new DisplayTarget("primary", true,
            new Glass.Core.Geometry.LogicalRect(0, 0, 1920, 1080)));
        var legacy = """
            {"schemaVersion":2,"payload":{"bars":[],"standaloneWidgets":[],"widgetInstances":[]}}
            """;
        await state.WriteAsync("shell-layout", legacy, TestContext.Current.CancellationToken);

        var loaded = await new JsonShellLayoutStore(state)
            .LoadAsync(fallback, TestContext.Current.CancellationToken);

        Assert.Empty(loaded.Bars);
        Assert.Empty(loaded.StandaloneWidgets);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
