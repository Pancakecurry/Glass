using Glass.Core.Placement;
using Glass.Core.Shell;
using Xunit;

namespace Glass.Core.Tests;

public sealed class BarDefinitionTests
{
    [Fact]
    public void DefaultBarIsBottomAnchoredHorizontalAndStructurallyZoned()
    {
        var bar = BarDefinition.CreateDefault(DisplayTarget.PrimaryFallback);

        var placement = Assert.IsType<AnchoredPlacement>(bar.Placement);
        Assert.Equal(ScreenEdge.Bottom, placement.Edge);
        Assert.Equal(BarOrientation.Horizontal, bar.Orientation);
        Assert.Equal(new[] { BarZone.Start, BarZone.Center, BarZone.End }, bar.Zones);
    }

    [Fact]
    public void FloatingPlacementDisablesAutoHideAndKeepsOneRunningSlot()
    {
        var source = BarDefinition.CreateDefault(DisplayTarget.PrimaryFallback) with
        {
            Placement = new FloatingPlacement(DisplayTarget.PrimaryFallback,
                new Glass.Core.Geometry.LogicalRect(10, 10, 400, 48)),
            AutoHideEnabled = true,
            Content = [
                new RunningApplicationsSlotBarItem(BarZone.Start),
                new RunningApplicationsSlotBarItem(BarZone.End),
            ],
        };
        var normalized = source.Normalize();
        Assert.False(normalized.AutoHideEnabled);
        Assert.Single(normalized.Content.OfType<RunningApplicationsSlotBarItem>());
    }

    [Fact]
    public void NormalizeClampsLengthAndThicknessWithoutChangingOrientation()
    {
        var source = BarDefinition.CreateDefault(DisplayTarget.PrimaryFallback) with
        {
            Orientation = BarOrientation.Vertical,
            Length = -1,
            Thickness = double.PositiveInfinity,
            Zones = [],
        };

        var normalized = source.Normalize();

        Assert.Equal(BarOrientation.Vertical, normalized.Orientation);
        Assert.Equal(BarDefinition.MinimumLength, normalized.Length);
        Assert.Equal(BarDefinition.MinimumThickness, normalized.Thickness);
        Assert.Equal(3, normalized.Zones.Count);
    }
}
