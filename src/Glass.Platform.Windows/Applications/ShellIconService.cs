using System.Runtime.InteropServices;
using Glass.Core.Applications;
using Microsoft.Win32.SafeHandles;

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

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern nuint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info,
        uint fileInfoSize, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);
}
