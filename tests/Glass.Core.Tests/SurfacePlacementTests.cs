using Glass.Core.Geometry;
using Glass.Core.Placement;
using Xunit;

namespace Glass.Core.Tests;

public sealed class SurfacePlacementTests
{
    [Fact]
    public void PlacementKindsCarryOnlyTheirValidData()
    {
        var target = DisplayTarget.PrimaryFallback;
        SurfacePlacement floating = new FloatingPlacement(
            target,
            new LogicalRect(10, 20, 300, 40));
        SurfacePlacement anchored = new AnchoredPlacement(
            target,
            ScreenEdge.Bottom,
            24,
            new LogicalSize(300, 40));
        SurfacePlacement docked = new DockedPlacement(
            target,
            ScreenEdge.Bottom,
            40);

        Assert.IsType<FloatingPlacement>(floating);
        Assert.Equal(24, Assert.IsType<AnchoredPlacement>(anchored).AlongEdgeOffset);
        Assert.Equal(40, Assert.IsType<DockedPlacement>(docked).Thickness);
    }

    [Fact]
    public void PlacementRejectsImpossibleDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FloatingPlacement(
                DisplayTarget.PrimaryFallback,
                new LogicalRect(0, 0, -1, 40)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DockedPlacement(DisplayTarget.PrimaryFallback, ScreenEdge.Left, 0));
    }
}
