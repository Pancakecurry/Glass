using Glass.Widgets.Abstractions;

namespace Glass.Widgets.BuiltIn;

public static class BuiltInWidgetCatalog
{
    private static readonly (string Id, string Name, string Description, WidgetSize Size,
        WidgetCapabilities Capabilities)[] Definitions =
    [
        ("media", "Media", "Controls the active Windows media session.", new(320, 120), Stateful),
        ("clock", "Clock", "Local or configured time zone clock.", new(180, 84), Multiple),
        ("date", "Date", "Locale-aware current date.", new(180, 84), Multiple),
        ("cpu", "CPU", "Current processor utilization.", new(180, 84), WidgetCapabilities.None),
        ("ram", "Memory", "Current physical memory utilization.", new(180, 84), WidgetCapabilities.None),
        ("network", "Network", "Current aggregate network throughput.", new(220, 96), WidgetCapabilities.None),
        ("storage", "Storage", "Volume capacity and free space.", new(220, 96), Multiple),
        ("battery", "Battery", "Power and charge status.", new(180, 84), WidgetCapabilities.None),
        ("audio", "Audio", "Default output volume and mute.", new(240, 96), WidgetCapabilities.None),
        ("calendar", "Calendar", "Local month browser.", new(320, 300), Multiple),
        ("timer", "Timer", "Persistent semantic countdown timer.", new(220, 120), Stateful),
        ("stopwatch", "Stopwatch", "Stopwatch with laps.", new(220, 140), Stateful),
        ("calculator", "Calculator", "Deterministic local expression calculator.", new(260, 340), Multiple),
        ("notes", "Quick Notes", "Local notes stored for this widget instance.", new(300, 240), Stateful),
        ("clipboard", "Clipboard", "Current Windows clipboard and optional system history.", new(300, 240), WidgetCapabilities.None),
        ("storageUtility", "Storage Utility", "Selects and opens local volumes.", new(300, 180), Multiple),
        ("shortcuts", "Shortcuts", "Launches user-selected apps, files, and folders.", new(280, 180), Stateful),
        ("weather", "Weather", "Optional MET Norway forecast for explicit coordinates.", new(280, 160), Stateful | WidgetCapabilities.RequiresNetwork),
        ("pomodoro", "Pomodoro", "Local focus and break timer.", new(220, 140), Stateful),
    ];

    private const WidgetCapabilities Multiple =
        WidgetCapabilities.MultipleInstances | WidgetCapabilities.HasConfiguration;
    private const WidgetCapabilities Stateful =
        WidgetCapabilities.MultipleInstances |
        WidgetCapabilities.HasPersistentState |
        WidgetCapabilities.HasConfiguration;

    public static IReadOnlyList<WidgetMetadata> All { get; } = Definitions
        .Select(definition => new WidgetMetadata(
            new WidgetTypeId(definition.Id),
            definition.Name,
            definition.Description,
            new WidgetSizeConstraints(
                new WidgetSize(120, 64),
                definition.Size,
                new WidgetSize(800, 800)),
            definition.Capabilities | WidgetCapabilities.Resizable))
        .ToArray();
}
