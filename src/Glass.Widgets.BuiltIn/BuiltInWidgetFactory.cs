using Glass.Widgets.Abstractions;
using Glass.Widgets.Runtime;

namespace Glass.Widgets.BuiltIn;

public sealed class BuiltInWidgetFactory : IWidgetFactory
{
    public BuiltInWidgetFactory(WidgetMetadata metadata) => Metadata = metadata;
    public WidgetMetadata Metadata { get; }
    public IWidgetInstance Create(WidgetInstanceConfiguration configuration) =>
        new BuiltInWidgetInstance(configuration);

    private sealed class BuiltInWidgetInstance(WidgetInstanceConfiguration configuration)
        : WidgetInstanceBase(configuration);
}

public static class BuiltInWidgetRegistration
{
    public static void RegisterAll(WidgetRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var metadata in BuiltInWidgetCatalog.All)
            registry.Register(new BuiltInWidgetFactory(metadata));
    }
}
