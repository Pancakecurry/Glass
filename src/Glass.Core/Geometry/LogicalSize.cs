namespace Glass.Core.Geometry;

public readonly record struct LogicalSize(double Width, double Height)
{
    public bool IsWellFormed =>
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0 &&
        Height > 0;
}
