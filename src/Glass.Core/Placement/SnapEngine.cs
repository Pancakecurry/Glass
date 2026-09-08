using Glass.Core.Geometry;

namespace Glass.Core.Placement;

public readonly record struct SnapResult(
    bool IsSnapped,
    ScreenEdge? Edge,
    double AlongEdgeOffset,
    LogicalRect Bounds);

public static class SnapEngine
{
    public static SnapResult Snap(
        LogicalRect candidate,
        LogicalRect workArea,
        double threshold)
    {
        if (!candidate.IsWellFormed || !workArea.IsWellFormed ||
            !double.IsFinite(threshold) || threshold < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidate),
                "Candidate, work area, and threshold must contain valid logical geometry.");
        }

        var fitted = candidate.ClampInside(workArea);
        var distances = new (ScreenEdge Edge, double Distance)[]
        {
            (ScreenEdge.Left, Math.Abs(candidate.X - workArea.X)),
            (ScreenEdge.Top, Math.Abs(candidate.Y - workArea.Y)),
            (ScreenEdge.Right, Math.Abs(candidate.Right - workArea.Right)),
            (ScreenEdge.Bottom, Math.Abs(candidate.Bottom - workArea.Bottom)),
        };

        var nearest = distances.OrderBy(item => item.Distance).First();
        if (nearest.Distance > threshold)
        {
            return new SnapResult(false, null, 0, fitted);
        }

        var snapped = nearest.Edge switch
        {
            ScreenEdge.Left => fitted with { X = workArea.X },
            ScreenEdge.Right => fitted with { X = workArea.Right - fitted.Width },
            ScreenEdge.Top => fitted with { Y = workArea.Y },
            ScreenEdge.Bottom => fitted with { Y = workArea.Bottom - fitted.Height },
            _ => throw new InvalidOperationException("Snap edge was not recognized."),
        };

        var offset = nearest.Edge is ScreenEdge.Top or ScreenEdge.Bottom
            ? (snapped.X + (snapped.Width / 2)) - (workArea.X + (workArea.Width / 2))
            : (snapped.Y + (snapped.Height / 2)) - (workArea.Y + (workArea.Height / 2));
        return new SnapResult(true, nearest.Edge, offset, snapped);
    }
}
