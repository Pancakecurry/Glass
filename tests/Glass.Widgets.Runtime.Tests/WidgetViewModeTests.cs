using Glass.Widgets.Abstractions;

namespace Glass.Widgets.Runtime.Tests;

public sealed class WidgetViewModeTests
{
    [Theory]
    [InlineData("media", 120, 70, WidgetViewMode.Compact)]
    [InlineData("media", 260, 120, WidgetViewMode.Standard)]
    [InlineData("media", 340, 220, WidgetViewMode.Expanded)]
    [InlineData("calendar", 180, 180, WidgetViewMode.Compact)]
    public void Select_UsesLogicalAvailableSize(
        string type, double width, double height, WidgetViewMode expected) =>
        Assert.Equal(expected, WidgetViewModeSelector.Select(
            new WidgetTypeId(type), new WidgetSize(width, height)));
}
