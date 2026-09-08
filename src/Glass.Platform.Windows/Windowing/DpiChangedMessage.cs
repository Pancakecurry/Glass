using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Glass.Platform.Windows.Windowing;

public readonly record struct DpiChangedMessage(
    uint DpiX,
    uint DpiY,
    RectInt32 SuggestedBounds)
{
    public static DpiChangedMessage Parse(nuint wParam, nint lParam)
    {
        if (lParam == 0)
        {
            throw new ArgumentException("WM_DPICHANGED did not provide a suggested RECT.", nameof(lParam));
        }

        var rectangle = Marshal.PtrToStructure<NativeRect>(lParam);
        return FromValues(
            unchecked((uint)(wParam & 0xFFFF)),
            unchecked((uint)((wParam >> 16) & 0xFFFF)),
            rectangle.Left,
            rectangle.Top,
            rectangle.Right,
            rectangle.Bottom);
    }

    public static DpiChangedMessage FromValues(
        uint dpiX,
        uint dpiY,
        int left,
        int top,
        int right,
        int bottom)
    {
        if (right <= left || bottom <= top)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "The suggested bounds must be non-empty.");
        }

        return new DpiChangedMessage(
            dpiX == 0 ? (uint)DpiConverter.DefaultDpi : dpiX,
            dpiY == 0 ? (uint)DpiConverter.DefaultDpi : dpiY,
            new RectInt32(left, top, checked(right - left), checked(bottom - top)));
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
