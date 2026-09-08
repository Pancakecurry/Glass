using System.ComponentModel;
using System.Runtime.InteropServices;
using Glass.Core.Shell;

namespace Glass.Platform.Windows.Windowing;

public static partial class ShellWindowStyle
{
    private const int GwlExStyle = -20;
    private const long WsExAppWindow = 0x00040000L;
    private const long WsExToolWindow = 0x00000080L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNotTopmost = new(-2);

    public static void Apply(nint hwnd, SurfaceZOrder zOrder)
    {
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        var desired = (style | WsExToolWindow) & ~WsExAppWindow;
        if (desired != style)
        {
            _ = SetWindowLongPtr(hwnd, GwlExStyle, new nint(desired));
        }

        if (!SetWindowPos(
                hwnd,
                zOrder == SurfaceZOrder.AlwaysOnTop ? HwndTopmost : HwndNotTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged | SwpNoOwnerZOrder))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint newValue);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
