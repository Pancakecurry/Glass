namespace Glass.Core.Applications;

public sealed record WindowCandidate(
    nint NativeWindow,
    string Title,
    ApplicationIdentity? Identity,
    bool IsVisible,
    bool IsTopLevel,
    bool IsToolWindow,
    bool IsCloaked,
    bool IsShellWindow,
    bool IsGlassWindow,
    bool IsForeground,
    bool IsMinimized);

public static class RunningApplicationRules
{
    public static bool ShouldInclude(WindowCandidate candidate) =>
        candidate.NativeWindow != 0 &&
        candidate.Identity is { IsValid: true } &&
        candidate.IsVisible &&
        candidate.IsTopLevel &&
        !candidate.IsToolWindow &&
        !candidate.IsCloaked &&
        !candidate.IsShellWindow &&
        !candidate.IsGlassWindow &&
        !string.IsNullOrWhiteSpace(candidate.Title);

    public static IReadOnlyList<RunningApplicationGroup> Group(
        IEnumerable<WindowCandidate> candidates,
        Func<ApplicationIdentity, string>? displayName = null) =>
        candidates
            .Where(ShouldInclude)
            .GroupBy(candidate => candidate.Identity!.Value)
            .Select(group => new RunningApplicationGroup(
                group.Key,
                displayName?.Invoke(group.Key) ?? group.Key.Value,
                group.Select(candidate => new RunningApplicationWindow(
                        candidate.NativeWindow,
                        group.Key,
                        candidate.Title,
                        candidate.IsForeground,
                        candidate.IsMinimized))
                    .OrderBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray()))
            .OrderBy(group => group.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
}
