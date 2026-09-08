namespace Glass.Core.Applications;

public enum ApplicationIdentityKind
{
    AppUserModelId,
    ShellParsingName,
    CanonicalExecutablePath,
}

public readonly record struct ApplicationIdentity(ApplicationIdentityKind Kind, string Value)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public override string ToString() => $"{Kind}:{Value}";
}

public sealed record ApplicationDescriptor(
    ApplicationIdentity Identity,
    string DisplayName,
    string? ExecutablePath = null,
    string? AppUserModelId = null,
    string? ShellParsingName = null);

public sealed record RunningApplicationWindow(
    nint NativeWindow,
    ApplicationIdentity Identity,
    string Title,
    bool IsForeground,
    bool IsMinimized);

public sealed record RunningApplicationGroup(
    ApplicationIdentity Identity,
    string DisplayName,
    IReadOnlyList<RunningApplicationWindow> Windows)
{
    public bool IsRunning => Windows.Count > 0;
    public bool IsActive => Windows.Any(window => window.IsForeground);
}
