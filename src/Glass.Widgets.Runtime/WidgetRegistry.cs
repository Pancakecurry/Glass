using Glass.Widgets.Abstractions;

namespace Glass.Widgets.Runtime;

public sealed class WidgetRegistry
{
    private readonly Dictionary<WidgetTypeId, IWidgetFactory> _factories = [];

    public IReadOnlyCollection<WidgetMetadata> Metadata =>
        _factories.Values.Select(factory => factory.Metadata).ToArray();

    public void Register(IWidgetFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (factory.Metadata.TypeId.IsEmpty)
        {
            throw new ArgumentException("A widget type ID is required.", nameof(factory));
        }

        if (!_factories.TryAdd(factory.Metadata.TypeId, factory))
        {
            throw new InvalidOperationException(
                $"Widget type '{factory.Metadata.TypeId}' is already registered.");
        }
    }

    public IWidgetInstance Create(WidgetInstanceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!_factories.TryGetValue(configuration.TypeId, out var factory))
        {
            throw new KeyNotFoundException($"Unknown widget type '{configuration.TypeId}'.");
        }

        return factory.Create(configuration with
        {
            Size = factory.Metadata.SizeConstraints.Clamp(configuration.Size),
        });
    }
}
