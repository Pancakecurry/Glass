using Glass.Core.Appearance;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Audio;
using Glass.Platform.Windows.Clipboard;
using Glass.Platform.Windows.Location;
using Glass.Platform.Windows.Media;
using Glass.Platform.Windows.SystemStatus;
using Glass.Widgets.BuiltIn.Weather;

namespace Glass.App.Runtime;

internal sealed record WidgetViewServices(
    SystemMediaSessionService Media,
    SystemMetricsProvider Metrics,
    PowerStatusProvider Power,
    AudioEndpointService Audio,
    ClipboardService Clipboard,
    ApplicationLaunchService Launcher,
    ShellIconService Icons,
    WindowsApplicationCatalog Applications,
    IWeatherProvider Weather,
    OneShotLocationService Location,
    Func<GlobalAppearanceSettings> Appearance);
