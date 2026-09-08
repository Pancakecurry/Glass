using Glass.Core.Shell;

namespace Glass.Core.Applications;

public static class TaskbarProjection
{
    public static IReadOnlyList<RunningApplicationGroup> UnpinnedRunningApplications(
        BarDefinition bar,
        IEnumerable<RunningApplicationGroup> running)
    {
        var pinned = bar.Content.OfType<PinnedApplicationBarItem>()
            .Select(item => item.Application)
            .ToHashSet();
        return running.Where(group => !pinned.Contains(group.Identity)).ToArray();
    }
}
