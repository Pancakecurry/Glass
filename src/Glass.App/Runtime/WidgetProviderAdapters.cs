using Glass.Platform.Windows.Audio;
using Glass.Platform.Windows.Clipboard;
using Glass.Platform.Windows.Media;
using Glass.Platform.Windows.SystemStatus;
using Glass.Widgets.Abstractions;

namespace Glass.App.Runtime;

internal sealed class DelegatingWidgetProvider(
    string providerId,
    Func<CancellationToken, ValueTask> start,
    Func<CancellationToken, ValueTask> stop) : IWidgetProvider
{
    public string ProviderId { get; } = providerId;
    public ValueTask StartAsync(CancellationToken cancellationToken = default) =>
        start(cancellationToken);
    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        stop(cancellationToken);
}

internal static class WidgetProviderAdapters
{
    public static IWidgetProvider Metrics(SystemMetricsProvider provider) =>
        new DelegatingWidgetProvider("systemMetrics", provider.StartAsync, provider.StopAsync);

    public static IWidgetProvider Power(PowerStatusProvider provider) =>
        new DelegatingWidgetProvider("power", provider.StartAsync, provider.StopAsync);

    public static IWidgetProvider Audio(AudioEndpointService provider) =>
        new DelegatingWidgetProvider("audio", provider.StartAsync, provider.StopAsync);

    public static IWidgetProvider Media(SystemMediaSessionService provider) =>
        new DelegatingWidgetProvider("media",
            token => new ValueTask(provider.StartAsync()),
            token => provider.StopAsync());

    public static IWidgetProvider Clipboard(ClipboardService provider) =>
        new DelegatingWidgetProvider("clipboard", provider.StartAsync, provider.StopAsync);

    public static IWidgetProvider Passive(string id) =>
        new DelegatingWidgetProvider(id,
            _ => ValueTask.CompletedTask,
            _ => ValueTask.CompletedTask);
}
