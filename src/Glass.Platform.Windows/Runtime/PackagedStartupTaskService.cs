using Windows.ApplicationModel;
using System.Diagnostics.CodeAnalysis;

namespace Glass.Platform.Windows.Runtime;

public enum StartupTaskAvailability { Available, Unavailable, DisabledByPolicy }

public sealed record StartupTaskSnapshot(
    StartupTaskAvailability Availability,
    bool IsEnabled);

public sealed class PackagedStartupTaskService
{
    public const string TaskId = "GlassStartup";

    [SuppressMessage("Performance", "CA1822", Justification =
        "The instance service is injected as the packaged startup-task boundary.")]
    public async ValueTask<StartupTaskSnapshot> GetAsync()
    {
        if (!PackageIdentityService.IsPackaged)
            return new(StartupTaskAvailability.Unavailable, false);
        var task = await StartupTask.GetAsync(TaskId);
        return From(task.State);
    }

    [SuppressMessage("Performance", "CA1822", Justification =
        "The instance service is injected as the packaged startup-task boundary.")]
    public async ValueTask<StartupTaskSnapshot> SetEnabledAsync(bool enabled)
    {
        if (!PackageIdentityService.IsPackaged)
            return new(StartupTaskAvailability.Unavailable, false);
        var task = await StartupTask.GetAsync(TaskId);
        if (!enabled)
        {
            task.Disable();
            return From(task.State);
        }
        var state = task.State == StartupTaskState.Enabled
            ? task.State
            : await task.RequestEnableAsync();
        return From(state);
    }

    private static StartupTaskSnapshot From(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => new(StartupTaskAvailability.Available, true),
        StartupTaskState.DisabledByPolicy or StartupTaskState.DisabledByUser =>
            new(StartupTaskAvailability.DisabledByPolicy, false),
        _ => new(StartupTaskAvailability.Available, false),
    };
}
