using System.Text.Json.Serialization;
using Glass.Core.Applications;

namespace Glass.Core.Shell;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PinnedApplicationBarItem), "pinnedApplication")]
[JsonDerivedType(typeof(WidgetBarItem), "widget")]
[JsonDerivedType(typeof(RunningApplicationsSlotBarItem), "runningApplications")]
[JsonDerivedType(typeof(SpacerBarItem), "spacer")]
public abstract record BarContentItem(BarZone Zone);

public sealed record PinnedApplicationBarItem(
    BarZone Zone,
    ApplicationIdentity Application) : BarContentItem(Zone);

public sealed record WidgetBarItem(
    BarZone Zone,
    Guid WidgetInstanceId) : BarContentItem(Zone);

public sealed record RunningApplicationsSlotBarItem(BarZone Zone) : BarContentItem(Zone);

public sealed record SpacerBarItem(
    BarZone Zone,
    bool IsFlexible,
    double LogicalSize = 8) : BarContentItem(Zone);

public sealed record StandaloneWidgetDefinition(
    Guid WidgetInstanceId,
    Placement.SurfacePlacement Placement,
    Geometry.LogicalSize Size,
    SurfaceZOrder ZOrder,
    bool IsEnabled);

public sealed record WidgetInstanceDefinition(
    Guid WidgetInstanceId,
    string WidgetTypeId,
    Geometry.LogicalSize Size,
    IReadOnlyDictionary<string, string> Configuration);
