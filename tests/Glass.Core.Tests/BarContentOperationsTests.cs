using Glass.Core.Applications;
using Glass.Core.Shell;

namespace Glass.Core.Tests;

public sealed class BarContentOperationsTests
{
    [Fact]
    public void Move_ReordersAndCanChangeZoneWithoutChangingItemIdentity()
    {
        var application = new ApplicationIdentity(
            ApplicationIdentityKind.AppUserModelId, "sample!app");
        IReadOnlyList<BarContentItem> content =
        [
            new PinnedApplicationBarItem(BarZone.Start, application),
            new SpacerBarItem(BarZone.Center, false, 12),
            new RunningApplicationsSlotBarItem(BarZone.Center),
        ];

        var moved = BarContentOperations.Move(content, 0, 2, BarZone.End);

        var pin = Assert.IsType<PinnedApplicationBarItem>(moved[2]);
        Assert.Equal(application, pin.Application);
        Assert.Equal(BarZone.End, pin.Zone);
    }
}
