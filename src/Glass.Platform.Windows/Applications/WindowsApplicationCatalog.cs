using System.Runtime.InteropServices;
using Glass.Core.Applications;

namespace Glass.Platform.Windows.Applications;

public sealed class WindowsApplicationCatalog
{
    public IReadOnlyList<ApplicationDescriptor> Current { get; private set; } = [];

    public event EventHandler? Refreshed;

    public IReadOnlyList<ApplicationDescriptor> Refresh()
    {
        Current = EnumerateCore();
        Refreshed?.Invoke(this, EventArgs.Empty);
        return Current;
    }

    public IReadOnlyList<ApplicationDescriptor> Enumerate()
        => Refresh();

    private static IReadOnlyList<ApplicationDescriptor> EnumerateCore()
    {
        var applications = new Dictionary<ApplicationIdentity, ApplicationDescriptor>();
        var shellType = Type.GetTypeFromProgID("Shell.Application") ??
            throw new PlatformNotSupportedException("Windows Shell automation is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType) ??
            throw new InvalidOperationException("Could not create the Windows Shell application object.");
        try
        {
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            if (folder is null) return [];
            foreach (dynamic item in folder.Items())
            {
                string name = item.Name as string ?? string.Empty;
                string path = item.Path as string ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path)) continue;
                var identity = SelectIdentity(path);
                applications[identity] = new ApplicationDescriptor(
                    identity,
                    name,
                    identity.Kind == ApplicationIdentityKind.CanonicalExecutablePath ? path : null,
                    identity.Kind == ApplicationIdentityKind.AppUserModelId ? identity.Value : null,
                    path);
            }
        }
        finally
        {
            if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }

        return applications.Values
            .OrderBy(application => application.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static ApplicationIdentity SelectIdentity(string parsingName)
    {
        if (parsingName.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            var value = parsingName["shell:AppsFolder\\".Length..];
            return new ApplicationIdentity(
                value.Contains('!') ? ApplicationIdentityKind.AppUserModelId : ApplicationIdentityKind.ShellParsingName,
                value);
        }
        if (Path.IsPathFullyQualified(parsingName))
            return new ApplicationIdentity(ApplicationIdentityKind.CanonicalExecutablePath,
                Path.GetFullPath(parsingName));
        return new ApplicationIdentity(ApplicationIdentityKind.ShellParsingName, parsingName);
    }
}
