using System.Globalization;

namespace Glass.Widgets.BuiltIn;

public sealed class CalendarModel(CultureInfo culture, DateOnly initialMonth)
{
    public DateOnly DisplayedMonth { get; private set; } = new(initialMonth.Year, initialMonth.Month, 1);
    public DateOnly? SelectedDate { get; private set; }
    public DayOfWeek FirstDayOfWeek => culture.DateTimeFormat.FirstDayOfWeek;
    public void PreviousMonth() => DisplayedMonth = DisplayedMonth.AddMonths(-1);
    public void NextMonth() => DisplayedMonth = DisplayedMonth.AddMonths(1);
    public void GoToToday(TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DisplayedMonth = new DateOnly(today.Year, today.Month, 1);
    }
    public void Select(DateOnly date) => SelectedDate = date;
}
