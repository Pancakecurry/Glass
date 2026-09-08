using Glass.Widgets.BuiltIn;
using Xunit;

namespace Glass.Widgets.BuiltIn.Tests;

public sealed class DeterministicWidgetLogicTests
{
    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("-(2 + 3)", -5)]
    [InlineData("25%", 0.25)]
    public void CalculatorUsesDeterministicGrammar(string expression, double expected) =>
        Assert.Equal(expected, CalculatorExpression.Evaluate(expression), 8);

    [Fact]
    public void CountdownSurvivesElapsedWallClockWithoutSleeping()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var timer = new CountdownTimer(time);
        timer.Start(TimeSpan.FromMinutes(5));
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining);
        timer.Pause();
        time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromMinutes(3), timer.Remaining);
    }

    [Fact]
    public void PomodoroSelectsLongBreakAfterFourthFocusInterval()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var pomodoro = new PomodoroTimer(time);
        for (var index = 0; index < 4; index++)
        {
            pomodoro.Start();
            pomodoro.Advance();
            if (index < 3) pomodoro.Advance();
        }
        Assert.Equal(PomodoroPhase.LongBreak, pomodoro.Phase);
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current += duration;
    }
}
