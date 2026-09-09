using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Glass.Core.Applications;
using Microsoft.Win32.SafeHandles;
using Windows.Graphics.Imaging;

namespace Glass.Platform.Windows.Applications;

public sealed partial class ShellIconService : IDisposable
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSmallIcon = 0x000000001;
    private readonly int _capacity;
    private readonly Dictionary<(ApplicationIdentity, int), Entry> _cache = [];
    private long _accessSequence;
    private bool _disposed;

    public ShellIconService(int capacity = 128) => _capacity = Math.Max(16, capacity);

    // The returned handle is borrowed and remains owned by this service.
    public nint GetIcon(ApplicationIdentity identity, int logicalSize, uint dpi)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var pixels = Math.Max(16, (int)Math.Round(logicalSize * dpi / 96d));
        var key = (identity, pixels);
        if (_cache.TryGetValue(key, out var existing))
        {
            existing.LastAccess = ++_accessSequence;
            return existing.Handle.DangerousGetHandle();
        }

        var target = identity.Kind == ApplicationIdentityKind.ShellParsingName
            ? $"shell:AppsFolder\\{identity.Value}"
            : identity.Value;
        var flags = ShgfiIcon | (pixels <= 20 ? ShgfiSmallIcon : ShgfiLargeIcon);
        _ = SHGetFileInfo(target, 0, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), flags);
        if (info.Icon == 0) return 0;
        var entry = new Entry(new SafeIconHandle(info.Icon), ++_accessSequence);
        _cache.Add(key, entry);
        Trim();
        return entry.Handle.DangerousGetHandle();
    }

    public SoftwareBitmap? GetBitmap(ApplicationIdentity identity, int logicalSize, uint dpi)
    {
        var icon = GetIcon(identity, logicalSize, dpi);
        if (icon == 0) return null;
        var pixels = Math.Max(16, (int)Math.Round(logicalSize * dpi / 96d));
        return CopyIconToBitmap(icon, pixels);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var entry in _cache.Values) entry.Handle.Dispose();
        _cache.Clear();
    }

    private void Trim()
    {
        while (_cache.Count > _capacity)
        {
            var oldest = _cache.MinBy(pair => pair.Value.LastAccess);
            if (_cache.Remove(oldest.Key, out var removed)) removed.Handle.Dispose();
        }
    }

    private static SoftwareBitmap? CopyIconToBitmap(nint icon, int size)
    {
        var info = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = size,
                Height = -size,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
            },
        };
        var screen = GetDC(0);
        if (screen == 0) return null;
        var memory = CreateCompatibleDC(screen);
        if (memory == 0)
        {
            _ = ReleaseDC(0, screen);
            return null;
        }

        var bitmap = CreateDIBSection(memory, ref info, 0, out var bits, 0, 0);
        if (bitmap == 0 || bits == 0)
        {
            if (bitmap != 0) _ = DeleteObject(bitmap);
            _ = DeleteDC(memory);
            _ = ReleaseDC(0, screen);
            return null;
        }

        var previous = SelectObject(memory, bitmap);
        try
        {
            _ = DrawIconEx(memory, 0, 0, icon, size, size, 0, 0, 0x0003);
            var pixelData = new byte[checked(size * size * 4)];
            Marshal.Copy(bits, pixelData, 0, pixelData.Length);
            return SoftwareBitmap.CreateCopyFromBuffer(
                pixelData.AsBuffer(), BitmapPixelFormat.Bgra8, size, size,
                BitmapAlphaMode.Premultiplied);
        }
        finally
        {
            if (previous != 0) _ = SelectObject(memory, previous);
            _ = DeleteObject(bitmap);
            _ = DeleteDC(memory);
            _ = ReleaseDC(0, screen);
        }
    }

    private sealed class Entry(SafeIconHandle handle, long lastAccess)
    {
        public SafeIconHandle Handle { get; } = handle;
        public long LastAccess { get; set; } = lastAccess;
    }

    private sealed class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeIconHandle(nint value) : base(true) => SetHandle(value);
        protected override bool ReleaseHandle() => DestroyIcon(handle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern nuint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info,
        uint fileInfoSize, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateDIBSection(
        nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint value);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DrawIconEx(
        nint dc, int x, int y, nint icon, int width, int height,
        uint step, nint brush, uint flags);
}
