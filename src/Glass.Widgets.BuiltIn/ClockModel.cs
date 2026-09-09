using System.Globalization;

namespace Glass.Widgets.BuiltIn;

public sealed record ClockOptions(string? TimeZoneId, bool Use24HourClock, bool ShowSeconds);

public sealed class ClockModel(TimeProvider timeProvider, ClockOptions options)
{
    private readonly TimeZoneInfo _timeZone = ResolveTimeZone(options.TimeZoneId);

    public DateTimeOffset Current =>
        TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _timeZone);

    public string TimeZoneDisplayName => _timeZone.DisplayName;

    public string FormatTime(CultureInfo culture)
    {
        var format = options.Use24HourClock
            ? (options.ShowSeconds ? "HH:mm:ss" : "HH:mm")
            : (options.ShowSeconds ? "h:mm:ss tt" : "h:mm tt");
        return Current.ToString(format, culture);
    }

    public TimeSpan UntilNextBoundary()
    {
        var now = Current;
        var ticks = options.ShowSeconds ? TimeSpan.TicksPerSecond : TimeSpan.TicksPerMinute;
        var remaining = ticks - (now.Ticks % ticks);
        return TimeSpan.FromTicks(remaining == 0 ? ticks : remaining);
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
