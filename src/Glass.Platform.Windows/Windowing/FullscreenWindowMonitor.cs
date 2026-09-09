using System.Runtime.InteropServices;
using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Runtime;

namespace Glass.Platform.Windows.Windowing;

public sealed record FullscreenWindowSnapshot(bool IsFullscreen, NativePixelRect DisplayBounds);

public sealed partial class FullscreenWindowMonitor : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint WineventOutofcontext = 0;
    private const uint DwmwaCloaked = 14;
    private const uint MonitorDefaultToNearest = 2;
    private readonly WinEventProcedure _callback;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private nint _foregroundHook;
    private nint _locationHook;
    private bool _disposed;

    public FullscreenWindowMonitor() => _callback = OnWinEvent;

    public event Action<FullscreenWindowSnapshot>? Changed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_foregroundHook != 0) return;
        _foregroundHook = SetWinEventHook(EventSystemForeground, EventSystemForeground,
            0, _callback, 0, 0, WineventOutofcontext);
        _locationHook = SetWinEventHook(EventObjectLocationChange, EventObjectLocationChange,
            0, _callback, 0, 0, WineventOutofcontext);
        if (_foregroundHook == 0 || _locationHook == 0)
        {
            if (_foregroundHook != 0) _ = UnhookWinEvent(_foregroundHook);
            if (_locationHook != 0) _ = UnhookWinEvent(_locationHook);
            _foregroundHook = 0;
            _locationHook = 0;
            throw new InvalidOperationException("Windows fullscreen event hooks could not be registered.");
        }
        Publish();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_foregroundHook != 0) _ = UnhookWinEvent(_foregroundHook);
        if (_locationHook != 0) _ = UnhookWinEvent(_locationHook);
        _foregroundHook = 0;
        _locationHook = 0;
        Changed = null;
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId,
        int childId, uint eventThread, uint eventTime)
    {
        if (_disposed || (eventType == EventObjectLocationChange && hwnd != GetForegroundWindow()))
            return;
        if (_context is null) Publish();
        else _context.Post(_ => Publish(), null);
    }

    private void Publish()
    {
        if (_disposed) return;
        var hwnd = GetForegroundWindow();
        if (hwnd == 0 || !GetWindowRect(hwnd, out var window) || IsIconic(hwnd))
        {
            Changed?.Invoke(new(false, default));
            return;
        }
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            Changed?.Invoke(new(false, default));
            return;
        }
        var cloaked = 0;
        _ = DwmGetWindowAttribute(hwnd, DwmwaCloaked, out cloaked, sizeof(int));
        _ = GetWindowThreadProcessId(hwnd, out var processId);
        var display = info.Monitor.ToNative();
        var decision = FullscreenPolicy.IsFullscreen(new ForegroundWindowGeometry(
            window.ToLogical(), info.Monitor.ToLogical(), processId == (uint)Environment.ProcessId,
            IsWindowVisible(hwnd), false, cloaked != 0));
        Changed?.Invoke(new(decision, display));
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventProcedure(nint hook, uint eventType, nint hwnd,
        int objectId, int childId, uint eventThread, uint eventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left; public int Top; public int Right; public int Bottom;
        public LogicalRect ToLogical() => new(Left, Top, Right - Left, Bottom - Top);
        public NativePixelRect ToNative() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    private static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint module,
        WinEventProcedure callback, uint processId, uint threadId, uint flags);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool UnhookWinEvent(nint hook);
    [LibraryImport("user32.dll")] private static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetWindowRect(nint hwnd, out NativeRect rectangle);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWindowVisible(nint hwnd);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsIconic(nint hwnd);
    [LibraryImport("user32.dll")] private static partial nint MonitorFromWindow(nint hwnd, uint flags);
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint hwnd, uint attribute, out int value, int size);
}
