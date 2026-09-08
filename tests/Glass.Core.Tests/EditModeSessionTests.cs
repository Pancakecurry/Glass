using Glass.Core.Editing;

namespace Glass.Core.Tests;

public sealed class EditModeSessionTests
{
    [Fact]
    public void Selection_EntersAndExitClearsState()
    {
        var session = new EditModeSession();
        var selection = new EditSelection(EditableSurfaceKind.Widget, Guid.NewGuid());

        session.Select(selection);
        Assert.True(session.IsActive);
        Assert.Equal(selection, session.Selection);

        session.Exit();
        Assert.False(session.IsActive);
        Assert.Null(session.Selection);
    }
}
