using Windows.ApplicationModel;
using Windows.Storage;

namespace Glass.Platform.Windows.Runtime;

public static class PackageIdentityService
{
    public static bool IsPackaged
    {
        get
        {
            try { _ = Package.Current.Id.Name; return true; }
            catch (InvalidOperationException) { return false; }
        }
    }

    public static string? TryGetLocalDataPath()
    {
        if (!IsPackaged) return null;
        try { return ApplicationData.Current.LocalFolder.Path; }
        catch (InvalidOperationException) { return null; }
    }
}
