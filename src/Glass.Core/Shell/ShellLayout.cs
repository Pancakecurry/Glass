using Glass.Core.Placement;

namespace Glass.Core.Shell;

public sealed record ShellLayout(IReadOnlyList<BarDefinition> Bars)
{
    public IReadOnlyList<StandaloneWidgetDefinition> StandaloneWidgets { get; init; } = [];
    public IReadOnlyList<WidgetInstanceDefinition> WidgetInstances { get; init; } = [];

    public static ShellLayout CreateDefault(DisplayTarget target) =>
        new([BarDefinition.CreateDefault(target)]);

    public ShellLayout Normalize()
    {
        var hosted = new HashSet<Guid>();
        var standalone = StandaloneWidgets
            .Where(widget => hosted.Add(widget.WidgetInstanceId))
            .ToArray();
        var bars = Bars.Select(bar =>
        {
            var normalized = bar.Normalize();
            return normalized with
            {
                Content = normalized.Content
                    .Where(item => item is not WidgetBarItem widget ||
                        hosted.Add(widget.WidgetInstanceId))
                    .ToArray(),
            };
        }).ToArray();
        var known = WidgetInstances
            .GroupBy(widget => widget.WidgetInstanceId)
            .Select(group => group.First())
            .Where(widget => hosted.Contains(widget.WidgetInstanceId))
            .ToArray();
        return new ShellLayout(bars)
        {
            StandaloneWidgets = standalone,
            WidgetInstances = known,
        };
    }
}
