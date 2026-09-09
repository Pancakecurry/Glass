using Glass.Widgets.Abstractions;
using Glass.Widgets.Runtime;
using Xunit;

namespace Glass.Widgets.Runtime.Tests;

public sealed class WidgetRuntimeTests
{
    [Fact]
    public async Task LifecycleTransitionsAreIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var instance = new TestInstance(Configuration());
        await instance.MountAsync(cancellationToken);
        await instance.MountAsync(cancellationToken);
        await instance.SetVisibleAsync(true, cancellationToken);
        await instance.SetVisibleAsync(true, cancellationToken);
        await instance.SetVisibleAsync(false, cancellationToken);
        Assert.Equal(1, instance.Mounts);
        Assert.Equal(2, instance.VisibilityChanges);
        Assert.Equal(WidgetLifecycleState.Suspended, instance.State);
        await instance.DisposeAsync();
        await instance.DisposeAsync();
        Assert.Equal(1, instance.Disposals);
    }

    [Fact]
    public async Task SharedProviderStartsAndStopsAtVisibilityBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var provider = new TestProvider();
        await using var coordinator = new ProviderCoordinator();
        coordinator.Register(provider);
        var one = WidgetInstanceId.New();
        var two = WidgetInstanceId.New();
        await coordinator.SetVisibleAsync("test", one, true, cancellationToken);
        await coordinator.SetVisibleAsync("test", two, true, cancellationToken);
        await coordinator.SetVisibleAsync("test", one, false, cancellationToken);
        Assert.Equal(1, provider.Starts);
        Assert.Equal(0, provider.Stops);
        await coordinator.SetVisibleAsync("test", two, false, cancellationToken);
        Assert.Equal(1, provider.Stops);
    }

    private static WidgetInstanceConfiguration Configuration() => new(
        WidgetInstanceId.New(), new WidgetTypeId("test"), new WidgetSize(100, 100),
        new Dictionary<string, string>());

    private sealed class TestInstance(WidgetInstanceConfiguration configuration)
        : WidgetInstanceBase(configuration)
    {
        public int Mounts { get; private set; }
        public int VisibilityChanges { get; private set; }
        public int Disposals { get; private set; }
        protected override ValueTask OnMountedAsync(CancellationToken token) { Mounts++; return ValueTask.CompletedTask; }
        protected override ValueTask OnVisibilityChangedAsync(bool visible, CancellationToken token)
        { VisibilityChanges++; return ValueTask.CompletedTask; }
        protected override ValueTask OnDisposedAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    private sealed class TestProvider : IWidgetProvider
    {
        public string ProviderId => "test";
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public ValueTask StartAsync(CancellationToken token = default) { Starts++; return ValueTask.CompletedTask; }
        public ValueTask StopAsync(CancellationToken token = default) { Stops++; return ValueTask.CompletedTask; }
    }
}
