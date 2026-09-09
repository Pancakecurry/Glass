using Glass.Widgets.Abstractions;
using Glass.Widgets.Runtime;

namespace Glass.Widgets.BuiltIn;

public interface IBuiltInWidgetInstance : IWidgetInstance
{
    event EventHandler? Updated;
}

public abstract class BuiltInWidgetInstanceBase(WidgetInstanceConfiguration configuration)
    : WidgetInstanceBase(configuration), IBuiltInWidgetInstance
{
    public event EventHandler? Updated;
    protected void Publish() => Updated?.Invoke(this, EventArgs.Empty);
    protected override ValueTask OnDisposedAsync()
    {
        Updated = null;
        return ValueTask.CompletedTask;
    }
}

public abstract class ProviderWidgetInstance(
    WidgetInstanceConfiguration configuration,
    ProviderCoordinator? providers,
    params string[] providerIds) : BuiltInWidgetInstanceBase(configuration)
{
    private bool _visible;

    protected override async ValueTask OnVisibilityChangedAsync(bool visible, CancellationToken token)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (providers is not null)
            foreach (var providerId in providerIds)
                await providers.SetVisibleAsync(
                    providerId, Configuration.InstanceId, visible, token).ConfigureAwait(false);
        Publish();
    }

    protected override async ValueTask OnDisposedAsync()
    {
        if (_visible && providers is not null)
            foreach (var providerId in providerIds)
                await providers.SetVisibleAsync(
                    providerId, Configuration.InstanceId, false).ConfigureAwait(false);
        await base.OnDisposedAsync().ConfigureAwait(false);
    }
}

public sealed class MediaWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "media");
public sealed class CpuWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class RamWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class NetworkWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class StorageWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class StorageUtilityWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "systemMetrics");
public sealed class BatteryWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "power");
public sealed class AudioWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "audio");
public sealed class ClipboardWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "clipboard");
public sealed class WeatherWidgetInstance(WidgetInstanceConfiguration c, ProviderCoordinator? p)
    : ProviderWidgetInstance(c, p, "weather");
