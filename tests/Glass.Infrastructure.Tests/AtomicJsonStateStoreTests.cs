using Glass.Infrastructure.Storage;
using Xunit;

namespace Glass.Infrastructure.Tests;

public sealed class AtomicJsonStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"glass-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReplacesExistingDocumentWithoutLeavingTemporaryFiles()
    {
        var paths = new GlassDataPaths(_root);
        var store = new AtomicJsonStateStore(paths);

        var cancellationToken = TestContext.Current.CancellationToken;
        await store.WriteAsync("settings", "{\"value\":1}", cancellationToken);
        await store.WriteAsync("settings", "{\"value\":2}", cancellationToken);

        Assert.Equal(
            "{\"value\":2}",
            await store.ReadAsync("settings", cancellationToken));
        Assert.Empty(Directory.EnumerateFiles(paths.StateDirectory, "*.tmp"));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("folder/file")]
    [InlineData("state.json")]
    public async Task RejectsPathTraversalAndFilenameSyntax(string key)
    {
        var store = new AtomicJsonStateStore(new GlassDataPaths(_root));
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await store.WriteAsync(key, "{}", cancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
