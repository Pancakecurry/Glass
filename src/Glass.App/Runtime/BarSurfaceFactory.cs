using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Applications;
using Glass.Shell.Surfaces;

namespace Glass.App.Runtime;

internal sealed class BarSurfaceFactory(
    WindowsDisplayService displays,
    RunningWindowTracker runningWindows,
    ApplicationLaunchService launcher,
    ProductSurfaceServices services) : IBarSurfaceFactory
{
    public IBarSurface Create(BarDefinition definition) =>
        new BarWindow(displays, runningWindows, launcher, services, definition);
}
