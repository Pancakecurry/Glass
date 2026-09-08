using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Glass.Platform.Windows.Windowing;

public sealed record WinUiWindowHandle(
    nint Hwnd,
    WindowId WindowId,
    AppWindow AppWindow)
{
    public static WinUiWindowHandle FromWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == 0)
        {
            throw new InvalidOperationException("WinUI did not provide a native window handle.");
        }

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId)
            ?? throw new InvalidOperationException("No AppWindow is associated with the WinUI window.");

        return new WinUiWindowHandle(hwnd, windowId, appWindow);
    }
}
