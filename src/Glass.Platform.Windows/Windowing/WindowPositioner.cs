using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Glass.Platform.Windows.Windowing;

public static partial class WindowPositioner
{
    private const uint DefaultDpi = 96;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;

    public static uint GetDpi(nint hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi == 0 ? DefaultDpi : dpi;
    }

    public static int DipsToPixels(double dips, uint dpi)
    {
        if (!double.IsFinite(dips) || dips < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dips));
        }

        return checked((int)Math.Round(dips * dpi / DefaultDpi));
    }

    public static void MoveAndResize(nint hwnd, RectInt32 bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds));
        }

        if (!MoveWindow(
                hwnd,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                true))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static void Show(nint hwnd) =>
        _ = ShowWindow(hwnd, SwShowNoActivate);

    public static void Hide(nint hwnd) =>
        _ = ShowWindow(hwnd, SwHide);

    public static RectInt32 CenterIn(RectInt32 area, int width, int height)
    {
        var fittedWidth = Math.Min(width, area.Width);
        var fittedHeight = Math.Min(height, area.Height);

        return new RectInt32(
            area.X + ((area.Width - fittedWidth) / 2),
            area.Y + ((area.Height - fittedHeight) / 2),
            fittedWidth,
            fittedHeight);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MoveWindow(
        nint hwnd,
        int x,
        int y,
        int width,
        int height,
        [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hwnd, int command);
}
