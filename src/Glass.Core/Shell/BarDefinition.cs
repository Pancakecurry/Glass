using Glass.Core.Geometry;
using Glass.Core.Placement;

namespace Glass.Core.Shell;

public readonly record struct BarId(Guid Value)
{
    public static BarId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum BarOrientation { Horizontal, Vertical }

public enum BarLengthMode { FitContent, Fixed, Fill }

public enum BarZone { Start, Center, End }

public enum SurfaceZOrder { Normal, AlwaysOnTop }

public sealed record BarDefinition(
    BarId Id,
    SurfacePlacement Placement,
    BarOrientation Orientation,
    BarLengthMode LengthMode,
    double Length,
    double Thickness,
    IReadOnlyList<BarZone> Zones,
    bool AutoHideEnabled,
    SurfaceZOrder ZOrder,
    bool IsEnabled)
{
    public IReadOnlyList<BarContentItem> Content { get; init; } =
        [new RunningApplicationsSlotBarItem(BarZone.Center)];

    public const double MinimumLength = 120;
    public const double MaximumLength = 4096;
    public const double MinimumThickness = 28;
    public const double MaximumThickness = 256;

    public BarDefinition Normalize()
    {
        ArgumentNullException.ThrowIfNull(Placement);
        var zones = Zones?.Distinct().ToArray() ?? [];
        if (zones.Length == 0)
        {
            zones = [BarZone.Start, BarZone.Center, BarZone.End];
        }

        var content = NormalizeContent(Content);
        return this with
        {
            Length = Math.Clamp(
                double.IsFinite(Length) ? Length : MinimumLength,
                MinimumLength,
                MaximumLength),
            Thickness = Math.Clamp(
                double.IsFinite(Thickness) ? Thickness : MinimumThickness,
                MinimumThickness,
                MaximumThickness),
            Zones = zones,
            Content = content,
            AutoHideEnabled = Placement is not FloatingPlacement && AutoHideEnabled,
        };
    }

    private static IReadOnlyList<BarContentItem> NormalizeContent(
        IReadOnlyList<BarContentItem>? content)
    {
        var items = content?.Where(item => item is not null).ToList() ?? [];
        var firstRunningSlot = items.FindIndex(item => item is RunningApplicationsSlotBarItem);
        if (firstRunningSlot < 0)
        {
            items.Add(new RunningApplicationsSlotBarItem(BarZone.Center));
        }
        else
        {
            for (var index = items.Count - 1; index > firstRunningSlot; index--)
            {
                if (items[index] is RunningApplicationsSlotBarItem)
                {
                    items.RemoveAt(index);
                }
            }
        }

        return items.ToArray();
    }

    public static BarDefinition CreateDefault(DisplayTarget target) =>
        new(
            BarId.New(),
            new AnchoredPlacement(
                target,
                ScreenEdge.Bottom,
                0.5,
                new LogicalSize(640, 48)),
            BarOrientation.Horizontal,
            BarLengthMode.FitContent,
            640,
            48,
            [BarZone.Start, BarZone.Center, BarZone.End],
            false,
            SurfaceZOrder.AlwaysOnTop,
            true);
}
