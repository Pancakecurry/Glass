namespace Glass.Core.Geometry;

/// <summary>
/// A platform-independent rectangle expressed in logical units.
/// </summary>
public readonly record struct LogicalRect(double X, double Y, double Width, double Height)
{
    public bool IsWellFormed =>
        double.IsFinite(X) &&
        double.IsFinite(Y) &&
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width >= 0 &&
        Height >= 0;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public LogicalRect ClampInside(LogicalRect bounds)
    {
        if (!IsWellFormed || !bounds.IsWellFormed)
        {
            throw new InvalidOperationException("Only well-formed rectangles can be clamped.");
        }

        var width = Math.Min(Width, bounds.Width);
        var height = Math.Min(Height, bounds.Height);
        return new LogicalRect(
            Math.Clamp(X, bounds.X, bounds.Right - width),
            Math.Clamp(Y, bounds.Y, bounds.Bottom - height),
            width,
            height);
    }
}
