namespace Glass.Core.Runtime;

public sealed record SessionHealthState
{
    public bool SessionActive { get; init; }
    public int ConsecutiveUncleanStarts { get; init; }
    public DateTimeOffset? LastStartedAt { get; init; }
    public DateTimeOffset? LastCleanShutdownAt { get; init; }
    public string? LastRecoveryReason { get; init; }
}

public sealed record SessionStartDecision(
    SessionHealthState NextState,
    bool EnterSafeMode,
    string? Reason);

public static class SessionHealthRules
{
    public const int SafeModeThreshold = 3;

    public static SessionStartDecision Begin(
        SessionHealthState? current,
        DateTimeOffset now,
        bool explicitSafeMode)
    {
        current ??= new SessionHealthState();
        var failures = current.SessionActive
            ? current.ConsecutiveUncleanStarts + 1
            : 0;
        var safe = explicitSafeMode || failures >= SafeModeThreshold;
        var reason = explicitSafeMode ? "Safe mode requested" :
            safe ? "Repeated unclean startup" : null;
        return new(current with
        {
            SessionActive = true,
            ConsecutiveUncleanStarts = failures,
            LastStartedAt = now,
            LastRecoveryReason = reason,
        }, safe, reason);
    }

    public static SessionHealthState Complete(
        SessionHealthState current,
        DateTimeOffset now) => current with
        {
            SessionActive = false,
            ConsecutiveUncleanStarts = 0,
            LastCleanShutdownAt = now,
        };
}
