using System.Globalization;

namespace Glass.Widgets.BuiltIn;

public sealed record ClockOptions(string? TimeZoneId, bool Use24HourClock, bool ShowSeconds);

public sealed class ClockModel(TimeProvider timeProvider, ClockOptions options)
{
    private readonly TimeZoneInfo _timeZone = string.IsNullOrWhiteSpace(options.TimeZoneId)
        ? TimeZoneInfo.Local
        : TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);

    public DateTimeOffset Current =>
        TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _timeZone);

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
}
