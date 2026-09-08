namespace Glass.Widgets.BuiltIn;

public sealed record CountdownState(
    TimeSpan Duration,
    TimeSpan RemainingWhenPaused,
    DateTimeOffset? EndsAtUtc)
{
    public bool IsRunning => EndsAtUtc is not null;
}

public sealed class CountdownTimer(TimeProvider timeProvider)
{
    public CountdownState State { get; private set; } =
        new(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), null);

    public TimeSpan Remaining
    {
        get
        {
            var remaining = State.EndsAtUtc is { } end
                ? end - timeProvider.GetUtcNow()
                : State.RemainingWhenPaused;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public void Start(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        State = new(duration, duration, timeProvider.GetUtcNow() + duration);
    }

    public void Pause() => State = State with { RemainingWhenPaused = Remaining, EndsAtUtc = null };
    public void Resume()
    {
        if (!State.IsRunning && State.RemainingWhenPaused > TimeSpan.Zero)
            State = State with { EndsAtUtc = timeProvider.GetUtcNow() + State.RemainingWhenPaused };
    }
    public void Reset() => State = new(State.Duration, State.Duration, null);
    public void Restore(CountdownState state) => State = state;
}

public sealed record StopwatchState(TimeSpan Accumulated, DateTimeOffset? StartedAtUtc,
    IReadOnlyList<TimeSpan> Laps);

public sealed class SemanticStopwatch(TimeProvider timeProvider)
{
    public StopwatchState State { get; private set; } = new(TimeSpan.Zero, null, []);
    public TimeSpan Elapsed => State.Accumulated +
        (State.StartedAtUtc is { } started ? timeProvider.GetUtcNow() - started : TimeSpan.Zero);
    public void Start() => State = State.StartedAtUtc is null
        ? State with { StartedAtUtc = timeProvider.GetUtcNow() }
        : State;
    public void Pause() => State = State.StartedAtUtc is null
        ? State
        : State with { Accumulated = Elapsed, StartedAtUtc = null };
    public void Lap() => State = State with { Laps = [.. State.Laps, Elapsed] };
    public void Reset() => State = new(TimeSpan.Zero, null, []);
}

public enum PomodoroPhase { Focus, ShortBreak, LongBreak }
public sealed class PomodoroTimer(TimeProvider timeProvider)
{
    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;
    public int CompletedFocusIntervals { get; private set; }
    public CountdownTimer Timer { get; } = new(timeProvider);
    public void Start() => Timer.Start(DurationFor(Phase));
    public void Advance()
    {
        if (Phase == PomodoroPhase.Focus) CompletedFocusIntervals++;
        Phase = Phase == PomodoroPhase.Focus
            ? (CompletedFocusIntervals % 4 == 0 ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak)
            : PomodoroPhase.Focus;
        Timer.Start(DurationFor(Phase));
    }
    private static TimeSpan DurationFor(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => TimeSpan.FromMinutes(25),
        PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(5),
        _ => TimeSpan.FromMinutes(15),
    };
}
