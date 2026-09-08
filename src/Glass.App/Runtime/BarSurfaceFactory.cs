using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Shell.Surfaces;

namespace Glass.App.Runtime;

internal sealed class BarSurfaceFactory(WindowsDisplayService displays) : IBarSurfaceFactory
{
    public IBarSurface Create(BarDefinition definition) =>
        new BarWindow(displays, definition);
}
