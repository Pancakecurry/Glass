using Glass.Core.Geometry;
using Windows.Graphics;

namespace Glass.Platform.Windows.Windowing;

public static class DpiConverter
{
    public const double DefaultDpi = 96;

    public static int ToPixels(double logicalUnits, uint dpi) =>
        checked((int)Math.Round(logicalUnits * Normalize(dpi) / DefaultDpi));

    public static double ToLogical(int pixels, uint dpi) =>
        pixels * DefaultDpi / Normalize(dpi);

    public static RectInt32 ToNative(
        LogicalRect relativeBounds,
        RectInt32 displayWorkArea,
        uint dpi) =>
        new(
            checked(displayWorkArea.X + ToPixels(relativeBounds.X, dpi)),
            checked(displayWorkArea.Y + ToPixels(relativeBounds.Y, dpi)),
            Math.Max(1, ToPixels(relativeBounds.Width, dpi)),
            Math.Max(1, ToPixels(relativeBounds.Height, dpi)));

    public static LogicalRect ToLogical(
        RectInt32 nativeBounds,
        RectInt32 displayWorkArea,
        uint dpi) =>
        new(
            ToLogical(nativeBounds.X - displayWorkArea.X, dpi),
            ToLogical(nativeBounds.Y - displayWorkArea.Y, dpi),
            ToLogical(nativeBounds.Width, dpi),
            ToLogical(nativeBounds.Height, dpi));

    private static uint Normalize(uint dpi) => dpi == 0 ? (uint)DefaultDpi : dpi;
}
