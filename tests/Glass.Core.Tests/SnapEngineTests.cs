using Glass.Core.Geometry;
using Glass.Core.Placement;
using Xunit;

namespace Glass.Core.Tests;

public sealed class SnapEngineTests
{
    public static TheoryData<LogicalRect, ScreenEdge> Edges => new()
    {
        { new LogicalRect(3, 100, 200, 40), ScreenEdge.Left },
        { new LogicalRect(500, 4, 200, 40), ScreenEdge.Top },
        { new LogicalRect(797, 100, 200, 40), ScreenEdge.Right },
        { new LogicalRect(500, 758, 200, 40), ScreenEdge.Bottom },
    };

    [Theory]
    [MemberData(nameof(Edges))]
    public void SnapsToEachEdge(LogicalRect candidate, ScreenEdge expected)
    {
        var result = SnapEngine.Snap(
            candidate,
            new LogicalRect(0, 0, 1000, 800),
            8);

        Assert.True(result.IsSnapped);
        Assert.Equal(expected, result.Edge);
    }

    [Fact]
    public void DoesNotSnapOutsideThreshold()
    {
        var result = SnapEngine.Snap(
            new LogicalRect(100, 100, 200, 40),
            new LogicalRect(0, 0, 1000, 800),
            8);

        Assert.False(result.IsSnapped);
    }

    [Fact]
    public void SupportsNegativeDesktopCoordinatesAndClampsOffset()
    {
        var result = SnapEngine.Snap(
            new LogicalRect(-1800, 1030, 400, 60),
            new LogicalRect(-1920, 0, 1920, 1080),
            24);

        Assert.True(result.IsSnapped);
        Assert.Equal(ScreenEdge.Bottom, result.Edge);
        Assert.Equal(-1800, result.Bounds.X);
        Assert.Equal(1020, result.Bounds.Y);
        Assert.Equal(-640, result.AlongEdgeOffset);
    }

    [Fact]
    public void ClampsAlongEdgePositionAndReportsTheClampedOffset()
    {
        var result = SnapEngine.Snap(
            new LogicalRect(900, 2, 300, 40),
            new LogicalRect(0, 0, 1000, 800),
            8);

        Assert.True(result.IsSnapped);
        Assert.Equal(ScreenEdge.Top, result.Edge);
        Assert.Equal(700, result.Bounds.X);
        Assert.Equal(350, result.AlongEdgeOffset);
    }
}
