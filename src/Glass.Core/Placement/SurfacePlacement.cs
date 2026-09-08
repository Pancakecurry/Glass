using System.Text.Json.Serialization;
using Glass.Core.Geometry;

namespace Glass.Core.Placement;

public enum ScreenEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

public readonly record struct NativePixelRect(int X, int Y, int Width, int Height);

public sealed record DisplayTarget(
    string PersistentId,
    bool WasPrimary,
    NativePixelRect LastKnownBounds)
{
    public static DisplayTarget PrimaryFallback { get; } =
        new("primary", true, new NativePixelRect(0, 0, 1920, 1080));
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(FloatingPlacement), "floating")]
[JsonDerivedType(typeof(AnchoredPlacement), "anchored")]
[JsonDerivedType(typeof(DockedPlacement), "docked")]
public abstract record SurfacePlacement
{
    protected SurfacePlacement(DisplayTarget target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public DisplayTarget Target { get; init; }
}

public sealed record FloatingPlacement : SurfacePlacement
{
    public FloatingPlacement(DisplayTarget target, LogicalRect bounds) : base(target)
    {
        if (!bounds.IsWellFormed || bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds));
        }

        Bounds = bounds;
    }

    public LogicalRect Bounds { get; init; }
}

public sealed record AnchoredPlacement : SurfacePlacement
{
    public AnchoredPlacement(
        DisplayTarget target,
        ScreenEdge edge,
        double alongEdgeOffset,
        LogicalSize size) : base(target)
    {
        if (!double.IsFinite(alongEdgeOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(alongEdgeOffset));
        }

        if (!size.IsWellFormed)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Edge = edge;
        AlongEdgeOffset = alongEdgeOffset;
        Size = size;
    }

    public ScreenEdge Edge { get; init; }
    public double AlongEdgeOffset { get; init; }
    public LogicalSize Size { get; init; }
}

public sealed record DockedPlacement : SurfacePlacement
{
    public DockedPlacement(DisplayTarget target, ScreenEdge edge, double thickness) : base(target)
    {
        if (!double.IsFinite(thickness) || thickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness));
        }

        Edge = edge;
        Thickness = thickness;
    }

    public ScreenEdge Edge { get; init; }
    public double Thickness { get; init; }
}
