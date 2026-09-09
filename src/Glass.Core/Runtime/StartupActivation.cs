namespace Glass.Core.Runtime;

public enum StartupActivationMode
{
    Normal,
    WindowsStartup,
    SafeMode,
}

public sealed record StartupActivation(
    StartupActivationMode Mode,
    bool OpenControlCenter,
    bool ForceSafeRendering)
{
    public static StartupActivation Parse(IEnumerable<string>? arguments)
    {
        var values = (arguments ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (values.Contains("--safe-mode"))
            return new(StartupActivationMode.SafeMode, true, true);
        if (values.Contains("--windows-startup"))
            return new(StartupActivationMode.WindowsStartup, false, false);
        return new(StartupActivationMode.Normal, true, false);
    }
}
