using Glass.Infrastructure.Recovery;
using Glass.Infrastructure.Storage;
using Xunit;

namespace Glass.Infrastructure.Tests;

public sealed class ConfigurationRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "glass-recovery-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Reset_BacksUpConfigurationAndPreservesWidgetContents()
    {
        var paths = new GlassDataPaths(_root);
        var store = new AtomicJsonStateStore(paths);
        await store.WriteAsync("settings", "{}", TestContext.Current.CancellationToken);
        await store.WriteAsync("shell-layout", "{}", TestContext.Current.CancellationToken);
        await store.WriteAsync("widget-notes", "personal", TestContext.Current.CancellationToken);

        var result = new ConfigurationRecoveryService(paths).ResetConfiguration();

        Assert.Equal(2, result.RemovedFiles);
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory, "settings.json")));
        Assert.True(File.Exists(Path.Combine(result.BackupDirectory, "shell-layout.json")));
        Assert.Equal("personal",
            await store.ReadAsync("widget-notes", TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
