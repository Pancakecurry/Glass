using Glass.App.Runtime;
using Glass.Widgets.Abstractions;
using Glass.Widgets.BuiltIn;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;

namespace Glass.App.Widgets;

internal sealed class BuiltInWidgetViewFactory(WidgetViewServices services)
{
    private readonly SystemWidgetViews _system = new(services);
    private readonly ProductivityWidgetViews _productivity = new();
    private readonly UtilityWidgetViews _utility = new(services);

    public FrameworkElement Create(IWidgetInstance instance, nint ownerWindow)
    {
        var mode = WidgetViewModeSelector.Select(
            instance.Configuration.TypeId, instance.Configuration.Size);
        var content = instance.Configuration.TypeId.Value switch
        {
            "media" => _system.Media(mode),
            "clock" => _system.Clock((ClockWidgetInstance)instance, mode),
            "date" => _system.Date((DateWidgetInstance)instance, mode),
            "cpu" => _system.Cpu(),
            "ram" => _system.Ram(),
            "network" => _system.Network(),
            "storage" => _system.Storage(single: true),
            "battery" => _system.Battery(),
            "audio" => _system.Audio(),
            "calendar" => _productivity.Calendar((CalendarWidgetInstance)instance),
            "timer" => _productivity.Timer((TimerWidgetInstance)instance),
            "stopwatch" => _productivity.Stopwatch((StopwatchWidgetInstance)instance),
            "calculator" => _productivity.Calculator((CalculatorWidgetInstance)instance),
            "notes" => _productivity.Notes((NotesWidgetInstance)instance),
            "clipboard" => _utility.Clipboard(),
            "storageUtility" => _system.Storage(single: false),
            "shortcuts" => _utility.Shortcuts((ShortcutsWidgetInstance)instance, ownerWindow),
            "weather" => _utility.Weather(instance.Configuration),
            "pomodoro" => _productivity.Pomodoro((PomodoroWidgetInstance)instance),
            _ => WidgetViewPrimitives.Unavailable(
                "Widget unavailable", "This built-in widget type is not registered."),
        };
        var metadata = BuiltInWidgetCatalog.All.First(item =>
            item.TypeId == instance.Configuration.TypeId);
        AutomationProperties.SetName(content, metadata.DisplayName);
        if (content is Microsoft.UI.Xaml.Controls.StackPanel panel)
            panel.Padding = new Thickness(services.Appearance().WidgetDensity switch
            {
                Glass.Core.Appearance.WidgetSurfaceDensity.Compact => 8,
                Glass.Core.Appearance.WidgetSurfaceDensity.Spacious => 16,
                _ => 12,
            });
        return content;
    }
}
