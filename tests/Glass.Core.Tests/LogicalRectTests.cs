using Glass.Core.Geometry;
using Xunit;

namespace Glass.Core.Tests;

public sealed class LogicalRectTests
{
    [Fact]
    public void WellFormedRectangleAcceptsFiniteNonNegativeDimensions()
    {
        var rectangle = new LogicalRect(10, 20, 320, 180);

        Assert.True(rectangle.IsWellFormed);
    }

    [Theory]
    [InlineData(double.NaN, 0, 10, 10)]
    [InlineData(0, double.PositiveInfinity, 10, 10)]
    [InlineData(0, 0, -1, 10)]
    [InlineData(0, 0, 10, -1)]
    public void WellFormedRectangleRejectsInvalidValues(
        double x,
        double y,
        double width,
        double height)
    {
        var rectangle = new LogicalRect(x, y, width, height);

        Assert.False(rectangle.IsWellFormed);
    }
}
