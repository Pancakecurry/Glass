using Glass.Core.Geometry;

namespace Glass.Core.Runtime;

public sealed record ForegroundWindowGeometry(
    LogicalRect WindowBounds,
    LogicalRect DisplayBounds,
    bool IsGlassWindow,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked);

public static class FullscreenPolicy
{
    private const double EdgeTolerance = 2;

    public static bool IsFullscreen(ForegroundWindowGeometry window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.IsGlassWindow || !window.IsVisible || window.IsMinimized || window.IsCloaked)
            return false;
        return Math.Abs(window.WindowBounds.X - window.DisplayBounds.X) <= EdgeTolerance &&
            Math.Abs(window.WindowBounds.Y - window.DisplayBounds.Y) <= EdgeTolerance &&
            Math.Abs(window.WindowBounds.Width - window.DisplayBounds.Width) <= EdgeTolerance &&
            Math.Abs(window.WindowBounds.Height - window.DisplayBounds.Height) <= EdgeTolerance;
    }
}
