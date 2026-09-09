using System.Runtime.InteropServices;
using Glass.Core.Applications;

namespace Glass.Platform.Windows.Applications;

public sealed partial class ApplicationLaunchService
{
    private const int SwShowNormal = 1;
    private const int SwRestore = 9;
    private const uint WmClose = 0x0010;

    public void Launch(ApplicationIdentity identity)
    {
        if (!identity.IsValid) throw new ArgumentException("A valid application identity is required.");
        if (identity.Kind == ApplicationIdentityKind.AppUserModelId)
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            _ = manager.ActivateApplication(identity.Value, null, 0, out _);
            return;
        }

        var target = identity.Kind == ApplicationIdentityKind.ShellParsingName &&
            !identity.Value.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                ? $"shell:AppsFolder\\{identity.Value}"
                : identity.Value;
        if ((nint)ShellExecute(0, "open", target, null, null, SwShowNormal) <= 32)
            throw new InvalidOperationException($"Windows could not launch '{target}'.");
    }

    public void ActivateOrToggle(
        RunningApplicationWindow window,
        bool minimizeForegroundWindow = true)
    {
        if (window.IsForeground && minimizeForegroundWindow)
        {
            _ = ShowWindow(window.NativeWindow, 6);
            return;
        }
        if (window.IsMinimized) _ = ShowWindow(window.NativeWindow, SwRestore);
        _ = SetForegroundWindow(window.NativeWindow);
    }

    public void RequestClose(RunningApplicationWindow window) =>
        _ = PostMessage(window.NativeWindow, WmClose, 0, 0);

    public void RequestCloseAll(IEnumerable<RunningApplicationWindow> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        foreach (var window in windows) RequestClose(window);
    }

    public void OpenSoundSettings() =>
        _ = ShellExecute(0, "open", "ms-settings:sound", null, null, SwShowNormal);

    public void OpenPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if ((nint)ShellExecute(0, "open", path, null, null, SwShowNormal) <= 32)
            throw new InvalidOperationException($"Windows could not open '{path}'.");
    }

    [LibraryImport("shell32.dll", EntryPoint = "ShellExecuteW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint ShellExecute(nint hwnd, string operation, string file,
        string? parameters, string? directory, int showCommand);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager;

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint processId);
        int ActivateForFile();
        int ActivateForProtocol();
    }
}
