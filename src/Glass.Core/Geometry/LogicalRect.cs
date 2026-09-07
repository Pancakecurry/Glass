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
}
