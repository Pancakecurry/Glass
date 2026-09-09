using Glass.Widgets.Abstractions;
using Glass.Widgets.Runtime;

namespace Glass.Widgets.BuiltIn;

public sealed class BuiltInWidgetFactory(
    WidgetMetadata metadata,
    ProviderCoordinator? providers = null,
    WidgetStateStore? stateStore = null) : IWidgetFactory
{
    public WidgetMetadata Metadata { get; } = metadata;

    public IWidgetInstance Create(WidgetInstanceConfiguration configuration) =>
        configuration.TypeId.Value switch
        {
            "media" => new MediaWidgetInstance(configuration, providers),
            "clock" => new ClockWidgetInstance(configuration),
            "date" => new DateWidgetInstance(configuration),
            "cpu" => new CpuWidgetInstance(configuration, providers),
            "ram" => new RamWidgetInstance(configuration, providers),
            "network" => new NetworkWidgetInstance(configuration, providers),
            "storage" => new StorageWidgetInstance(configuration, providers),
            "battery" => new BatteryWidgetInstance(configuration, providers),
            "audio" => new AudioWidgetInstance(configuration, providers),
            "calendar" => new CalendarWidgetInstance(configuration),
            "timer" => new TimerWidgetInstance(configuration),
            "stopwatch" => new StopwatchWidgetInstance(configuration),
            "calculator" => new CalculatorWidgetInstance(configuration),
            "notes" => new NotesWidgetInstance(configuration, RequireStateStore()),
            "clipboard" => new ClipboardWidgetInstance(configuration, providers),
            "storageUtility" => new StorageUtilityWidgetInstance(configuration, providers),
            "shortcuts" => new ShortcutsWidgetInstance(configuration, RequireStateStore()),
            "weather" => new WeatherWidgetInstance(configuration, providers),
            "pomodoro" => new PomodoroWidgetInstance(configuration),
            _ => throw new ArgumentOutOfRangeException(
                nameof(configuration), configuration.TypeId, "Unknown built-in widget type."),
        };

    private WidgetStateStore RequireStateStore() => stateStore ??
        throw new InvalidOperationException("This widget requires a local state store.");
}

public static class BuiltInWidgetRegistration
{
    public static void RegisterAll(
        WidgetRegistry registry,
        ProviderCoordinator? providers = null,
        WidgetStateStore? stateStore = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var metadata in BuiltInWidgetCatalog.All)
            registry.Register(new BuiltInWidgetFactory(metadata, providers, stateStore));
    }
}
