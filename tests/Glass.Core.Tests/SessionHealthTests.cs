using Glass.Core.Runtime;
using Xunit;

namespace Glass.Core.Tests;

public sealed class SessionHealthTests
{
    [Fact]
    public void RepeatedUncleanStarts_EventuallyEnterSafeMode()
    {
        var now = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        SessionHealthState state = new();
        SessionStartDecision decision = SessionHealthRules.Begin(state, now, false);

        for (var attempt = 0; attempt < SessionHealthRules.SafeModeThreshold; attempt++)
            decision = SessionHealthRules.Begin(decision.NextState, now.AddMinutes(attempt + 1), false);

        Assert.True(decision.EnterSafeMode);
        Assert.Equal("Repeated unclean startup", decision.Reason);
    }

    [Fact]
    public void CleanCompletion_ResetsFailureCount()
    {
        var state = new SessionHealthState
        {
            SessionActive = true,
            ConsecutiveUncleanStarts = 2,
        };

        var completed = SessionHealthRules.Complete(state, DateTimeOffset.UnixEpoch);

        Assert.False(completed.SessionActive);
        Assert.Equal(0, completed.ConsecutiveUncleanStarts);
    }

    [Fact]
    public void ExplicitSafeMode_DoesNotRequireFailures()
    {
        var decision = SessionHealthRules.Begin(new(), DateTimeOffset.UnixEpoch, true);

        Assert.True(decision.EnterSafeMode);
        Assert.Equal("Safe mode requested", decision.Reason);
    }
}
