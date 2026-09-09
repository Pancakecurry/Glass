using Glass.Core.Accessibility;
using Xunit;

namespace Glass.Core.Tests;

public sealed class AccessibilityCommandTests
{
    [Fact]
    public void CoarseEditCommand_UsesTenDipStep()
    {
        Assert.Equal(10, new EditCommand(EditCommandKind.NudgeRight, true).Delta);
        Assert.Equal(1, new EditCommand(EditCommandKind.NudgeRight, false).Delta);
    }
}
