using Glass.App.Widgets;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Platform.Windows.Applications;
using Glass.Rendering.Materials;
using Glass.Rendering.Motion;
using Glass.Shell.Runtime;
using Glass.Widgets.Runtime;

namespace Glass.App.Runtime;

internal sealed record ProductSurfaceServices(
    Func<ShellRuntime> Shell,
    Func<GlassSettings> Settings,
    WidgetRuntime WidgetRuntime,
    BuiltInWidgetViewFactory WidgetViews,
    ShellIconService Icons,
    WindowsApplicationCatalog Applications,
    GlassMaterialController Materials,
    GlassMotionController Motion,
    EditModeSession EditMode,
    Action ShowControlCenter,
    Action SyncWidgetSurfaces);
