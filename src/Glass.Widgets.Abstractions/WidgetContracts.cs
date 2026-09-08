namespace Glass.Widgets.Abstractions;

/// <summary>
/// Stable identifier for a built-in widget type.
/// This is a contract primitive, not a third-party plugin registration API.
/// </summary>
public readonly record struct WidgetTypeId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

/// <summary>
/// Identifier for one configured widget instance.
/// </summary>
public readonly record struct WidgetInstanceId(Guid Value)
{
    public static WidgetInstanceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Logical widget dimensions. Platform-specific pixel conversion belongs elsewhere.
/// </summary>
public readonly record struct WidgetSize(double Width, double Height)
{
    public bool IsWellFormed =>
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0 &&
        Height > 0;
}

/// <summary>
/// Minimum and maximum logical dimensions a widget can request.
/// </summary>
public readonly record struct WidgetSizeConstraints(
    WidgetSize Minimum,
    WidgetSize Default,
    WidgetSize Maximum)
{
    public WidgetSize Clamp(WidgetSize value) => new(
        Math.Clamp(value.Width, Minimum.Width, Maximum.Width),
        Math.Clamp(value.Height, Minimum.Height, Maximum.Height));
}

[Flags]
public enum WidgetCapabilities
{
    None = 0,
    Resizable = 1 << 0,
    MultipleInstances = 1 << 1,
    RequiresNetwork = 1 << 2,
    HasPersistentState = 1 << 3,
    HasConfiguration = 1 << 4,
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
    WidgetSize Size,
    IReadOnlyDictionary<string, string> Settings);

public enum WidgetLifecycleState
{
    Created,
    Mounted,
    Visible,
    Suspended,
    Disposed,
}

public interface IWidgetInstance : IAsyncDisposable
{
    WidgetInstanceConfiguration Configuration { get; }
    WidgetLifecycleState State { get; }
    ValueTask MountAsync(CancellationToken cancellationToken = default);
    ValueTask SetVisibleAsync(bool visible, CancellationToken cancellationToken = default);
}

public interface IWidgetFactory
{
    WidgetMetadata Metadata { get; }
    IWidgetInstance Create(WidgetInstanceConfiguration configuration);
}

public interface IWidgetProvider
{
    string ProviderId { get; }
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
