using Glass.Widgets.BuiltIn;
using Xunit;

namespace Glass.Widgets.BuiltIn.Tests;

public sealed class BuiltInLogicTests
{
    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("-(10 - 4) / 2", -3)]
    [InlineData("25%", 0.25)]
    public void CalculatorUsesDeterministicGrammar(string expression, double expected) =>
        Assert.Equal(expected, CalculatorExpression.Evaluate(expression), 8);

    [Fact]
    public void CountdownUsesSemanticTimestampAcrossElapsedTime()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var timer = new CountdownTimer(time);
        timer.Start(TimeSpan.FromMinutes(5));
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining);
        timer.Pause();
        time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining);
    }

    [Fact]
    public void PomodoroUsesSharedSemanticTimerPrimitives()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var pomodoro = new PomodoroTimer(time);
        pomodoro.Start();
        time.Advance(TimeSpan.FromMinutes(25));
        Assert.Equal(TimeSpan.Zero, pomodoro.Timer.Remaining);
        pomodoro.Advance();
        Assert.Equal(PomodoroPhase.ShortBreak, pomodoro.Phase);
    }

    [Fact]
    public void ClockFallsBackToLocalForUnknownTimeZone()
    {
        var clock = new ClockModel(TimeProvider.System,
            new ClockOptions("not-a-real-time-zone", true, false));

        Assert.Equal(TimeZoneInfo.Local.DisplayName, clock.TimeZoneDisplayName);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
