using Glass.Core.Placement;
using Xunit;

namespace Glass.Core.Tests;

public sealed class SurfacePlacementTests
{
    [Fact]
    public void FloatingPlacementHasNoEdge()
    {
        var placement = SurfacePlacement.Floating;

        Assert.Equal(SurfacePlacementMode.Floating, placement.Mode);
        Assert.Null(placement.Edge);
    }

    [Theory]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Bottom)]
    [InlineData(DockEdge.Left)]
    [InlineData(DockEdge.Right)]
    public void DockedPlacementCarriesItsEdge(DockEdge edge)
    {
        var placement = SurfacePlacement.Docked(edge);

        Assert.Equal(SurfacePlacementMode.Docked, placement.Mode);
        Assert.Equal(edge, placement.Edge);
    }
}
