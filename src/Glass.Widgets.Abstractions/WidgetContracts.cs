namespace Glass.Widgets.Abstractions;

/// <summary>
/// Stable identifier for a built-in widget type.
/// This is a contract primitive, not a third-party plugin registration API.
/// </summary>
public readonly record struct WidgetTypeId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
}

/// <summary>
/// Identifier for one configured widget instance.
/// </summary>
public readonly record struct WidgetInstanceId(Guid Value)
{
    public static WidgetInstanceId New() => new(Guid.NewGuid());
}

/// <summary>
/// Logical widget dimensions. Platform-specific pixel conversion belongs elsewhere.
/// </summary>
public readonly record struct WidgetSize(double Width, double Height)
{
    public bool IsWellFormed =>
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width >= 0 &&
        Height >= 0;
}

/// <summary>
/// Minimum and maximum logical dimensions a widget can request.
/// </summary>
public readonly record struct WidgetSizeConstraints(
    WidgetSize Minimum,
    WidgetSize Maximum);

[Flags]
public enum WidgetCapabilities
{
    None = 0,
    Resizable = 1 << 0,
    MultipleInstances = 1 << 1,
    RequiresNetwork = 1 << 2,
}

/// <summary>
/// Describes a trusted widget without prescribing its UI implementation.
/// </summary>
public sealed record WidgetMetadata(
    WidgetTypeId TypeId,
    string DisplayName,
    string Description,
    WidgetSizeConstraints SizeConstraints,
    WidgetCapabilities Capabilities);

/// <summary>
/// Serializable instance configuration owned by the local application state.
/// Values remain intentionally opaque at this layer.
/// </summary>
public sealed record WidgetInstanceConfiguration(
    WidgetInstanceId InstanceId,
    WidgetTypeId TypeId,
    IReadOnlyDictionary<string, string> Settings);
