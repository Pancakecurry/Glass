using System.Globalization;
using Glass.Widgets.Abstractions;

namespace Glass.Widgets.BuiltIn;

public class ClockWidgetInstance : BuiltInWidgetInstanceBase
{
    private CancellationTokenSource? _ticker;


    public ClockWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration)
    {
        var settings = configuration.Settings;
        Model = new ClockModel(TimeProvider.System, new ClockOptions(
            settings.GetValueOrDefault("timeZoneId"),
            !settings.TryGetValue("use24Hour", out var value) || value != "false",
            settings.GetValueOrDefault("showSeconds") == "true"));
    }

    public ClockModel Model { get; }

    protected override ValueTask OnVisibilityChangedAsync(bool visible, CancellationToken token)
    {
        _ticker?.Cancel();
        _ticker?.Dispose();
        _ticker = visible ? CancellationTokenSource.CreateLinkedTokenSource(token) : null;
        if (_ticker is not null) ObserveTickerAsync(_ticker.Token);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnDisposedAsync()
    {
        _ticker?.Cancel();
        _ticker?.Dispose();
        return base.OnDisposedAsync();
    }

    private async void ObserveTickerAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Publish();
                await Task.Delay(Model.UntilNextBoundary(), TimeProvider.System, token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Clock update failed: {exception}");
        }
    }
}

public sealed class DateWidgetInstance(WidgetInstanceConfiguration configuration)
    : ClockWidgetInstance(configuration);

public sealed class CalendarWidgetInstance : BuiltInWidgetInstanceBase
{
    public CalendarWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) =>
        Model = new CalendarModel(CultureInfo.CurrentCulture, DateOnly.FromDateTime(DateTime.Today));

    public CalendarModel Model { get; }
    public void Previous() { Model.PreviousMonth(); Publish(); }
    public void Next() { Model.NextMonth(); Publish(); }
    public void Today() { Model.GoToToday(TimeProvider.System); Publish(); }
    public void Select(DateOnly date) { Model.Select(date); Publish(); }
}

public sealed class TimerWidgetInstance : BuiltInWidgetInstanceBase
{
    public TimerWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) { }
    public CountdownTimer Timer { get; } = new(TimeProvider.System);
    public void Start(TimeSpan duration) { Timer.Start(duration); Publish(); }
    public void Pause() { Timer.Pause(); Publish(); }
    public void Resume() { Timer.Resume(); Publish(); }
    public void Reset() { Timer.Reset(); Publish(); }
}

public sealed class StopwatchWidgetInstance : BuiltInWidgetInstanceBase
{
    public StopwatchWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) { }
    public SemanticStopwatch Stopwatch { get; } = new(TimeProvider.System);
    public void Start() { Stopwatch.Start(); Publish(); }
    public void Pause() { Stopwatch.Pause(); Publish(); }
    public void Lap() { Stopwatch.Lap(); Publish(); }
    public void Reset() { Stopwatch.Reset(); Publish(); }
}

public sealed class PomodoroWidgetInstance : BuiltInWidgetInstanceBase
{
    public PomodoroWidgetInstance(WidgetInstanceConfiguration configuration) : base(configuration) =>
        Pomodoro = new PomodoroTimer(TimeProvider.System, new PomodoroOptions(
            TimeSpan.FromMinutes(ReadMinutes(configuration.Settings, "focusMinutes", 25)),
            TimeSpan.FromMinutes(ReadMinutes(configuration.Settings, "shortBreakMinutes", 5)),
            TimeSpan.FromMinutes(ReadMinutes(configuration.Settings, "longBreakMinutes", 15))));

    public PomodoroTimer Pomodoro { get; }
    public void Start() { Pomodoro.Start(); Publish(); }
    public void Pause() { Pomodoro.Timer.Pause(); Publish(); }
    public void Reset() { Pomodoro.Timer.Reset(); Publish(); }
    public void Advance() { Pomodoro.Advance(); Publish(); }

    private static double ReadMinutes(
        IReadOnlyDictionary<string, string> settings, string key, double fallback) =>
        settings.TryGetValue(key, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 1, 180) : fallback;
}
