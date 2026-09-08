namespace Glass.Core.Placement;

public enum SurfacePlacementMode
{
    Floating,
    Docked,
}

public enum DockEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

public readonly record struct SurfacePlacement(
    SurfacePlacementMode Mode,
    DockEdge? Edge)
{
    public static SurfacePlacement Floating =>
        new(SurfacePlacementMode.Floating, null);

    public static SurfacePlacement Docked(DockEdge edge) =>
        new(SurfacePlacementMode.Docked, edge);
}
