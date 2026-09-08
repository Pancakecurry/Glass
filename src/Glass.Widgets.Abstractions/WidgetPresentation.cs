namespace Glass.Widgets.Abstractions;

public enum WidgetViewMode { Compact, Standard, Expanded }

public static class WidgetViewModeSelector
{
    public static WidgetViewMode Select(WidgetTypeId typeId, WidgetSize size)
    {
        if (!size.IsWellFormed) return WidgetViewMode.Compact;
        var compactBoundary = typeId.Value is "calendar" or "calculator" ? 220 : 150;
        if (size.Width < compactBoundary || size.Height < 84) return WidgetViewMode.Compact;
        if (size.Width >= 300 && size.Height >= 180) return WidgetViewMode.Expanded;
        return WidgetViewMode.Standard;
    }
}
