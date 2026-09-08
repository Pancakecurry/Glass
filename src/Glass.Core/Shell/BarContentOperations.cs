namespace Glass.Core.Shell;

public static class BarContentOperations
{
    public static IReadOnlyList<BarContentItem> Move(
        IReadOnlyList<BarContentItem> content,
        int fromIndex,
        int toIndex,
        BarZone? targetZone = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if ((uint)fromIndex >= (uint)content.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if ((uint)toIndex >= (uint)content.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));
        if (fromIndex == toIndex && targetZone is null) return content.ToArray();

        var items = content.ToList();
        var moved = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, targetZone is { } zone ? WithZone(moved, zone) : moved);
        return items;
    }

    public static BarContentItem WithZone(BarContentItem item, BarZone zone) => item switch
    {
        PinnedApplicationBarItem value => value with { Zone = zone },
        WidgetBarItem value => value with { Zone = zone },
        RunningApplicationsSlotBarItem value => value with { Zone = zone },
        SpacerBarItem value => value with { Zone = zone },
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };
}
