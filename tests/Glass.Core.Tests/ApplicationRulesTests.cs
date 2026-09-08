using Glass.Core.Applications;
using Xunit;

namespace Glass.Core.Tests;

public sealed class ApplicationRulesTests
{
    private static readonly ApplicationIdentity Identity =
        new(ApplicationIdentityKind.CanonicalExecutablePath, @"C:\Apps\Example.exe");

    [Fact]
    public void FilteringRejectsToolCloakedShellAndGlassWindows()
    {
        var candidate = Candidate();
        Assert.True(RunningApplicationRules.ShouldInclude(candidate));
        Assert.False(RunningApplicationRules.ShouldInclude(candidate with { IsToolWindow = true }));
        Assert.False(RunningApplicationRules.ShouldInclude(candidate with { IsCloaked = true }));
        Assert.False(RunningApplicationRules.ShouldInclude(candidate with { IsShellWindow = true }));
        Assert.False(RunningApplicationRules.ShouldInclude(candidate with { IsGlassWindow = true }));
    }

    [Fact]
    public void GroupingUsesStableIdentityRatherThanWindowHandle()
    {
        var groups = RunningApplicationRules.Group([
            Candidate() with { NativeWindow = 1, Title = "One" },
            Candidate() with { NativeWindow = 2, Title = "Two" },
        ]);
        var group = Assert.Single(groups);
        Assert.Equal(Identity, group.Identity);
        Assert.Equal(2, group.Windows.Count);
    }

    private static WindowCandidate Candidate() => new(
        1, "Example", Identity, true, true, false, false, false, false, false, false);
}
