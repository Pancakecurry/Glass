using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Glass.Core.Applications;

namespace Glass.Platform.Windows.Applications;

public sealed partial class RunningWindowTracker : IDisposable
{
    private const uint EventMin = 0x0003;
    private const uint EventMax = 0x800B;
    private const uint WineventOutofcontext = 0;
    private const int GwlExstyle = -20;
    private const long WsExToolwindow = 0x00000080L;
    private const uint DwmwaCloaked = 14;
    private readonly HashSet<nint> _glassWindows;
    private readonly WinEventProcedure _callback;
    private readonly SynchronizationContext? _context;
    private readonly Timer _coalesceTimer;
    private nint _hook;
    private bool _disposed;

    public RunningWindowTracker(IEnumerable<nint>? glassWindows = null)
    {
        _glassWindows = glassWindows?.ToHashSet() ?? [];
        _callback = OnWinEvent;
        _context = SynchronizationContext.Current;
        _coalesceTimer = new Timer(_ => PublishSnapshot(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public IReadOnlyList<RunningApplicationGroup> Current { get; private set; } = [];
    public event EventHandler? Changed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hook != 0) return;
        _hook = SetWinEventHook(EventMin, EventMax, 0, _callback, 0, 0, WineventOutofcontext);
        if (_hook == 0) throw new InvalidOperationException("SetWinEventHook failed.");
        PublishSnapshot();
    }

    public void RegisterGlassWindow(nint hwnd) => _glassWindows.Add(hwnd);

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PublishSnapshot();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != 0) _ = UnhookWinEvent(_hook);
        _hook = 0;
        _coalesceTimer.Dispose();
        Changed = null;
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId,
        int childId, uint eventThread, uint eventTime)
    {
        if (_disposed || hwnd == 0 || objectId != 0) return;
        _coalesceTimer.Change(75, Timeout.Infinite);
    }

    private void PublishSnapshot()
    {
        if (_disposed) return;
        var candidates = new List<WindowCandidate>();
        _ = EnumWindows((hwnd, _) =>
        {
            candidates.Add(CreateCandidate(hwnd));
            return true;
        }, 0);
        var groups = RunningApplicationRules.Group(candidates);
        void Apply()
        {
            if (_disposed) return;
            Current = groups;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        if (_context is null) Apply();
        else _context.Post(_ => Apply(), null);
    }

    private WindowCandidate CreateCandidate(nint hwnd)
    {
        var title = new StringBuilder(512);
        _ = GetWindowText(hwnd, title, title.Capacity);
        _ = GetWindowThreadProcessId(hwnd, out var processId);
        var identity = TryGetIdentity(processId);
        var cloaked = 0;
        _ = DwmGetWindowAttribute(hwnd, DwmwaCloaked, out cloaked, sizeof(int));
        var owner = GetWindow(hwnd, 4);
        return new WindowCandidate(
            hwnd,
            title.ToString(),
            identity,
            IsWindowVisible(hwnd),
            owner == 0,
            (GetWindowLongPtr(hwnd, GwlExstyle).ToInt64() & WsExToolwindow) != 0,
            cloaked != 0,
            hwnd == GetShellWindow(),
            _glassWindows.Contains(hwnd),
            hwnd == GetForegroundWindow(),
            IsIconic(hwnd));
    }

    private static ApplicationIdentity? TryGetIdentity(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            var path = process.MainModule?.FileName;
            return string.IsNullOrWhiteSpace(path)
                ? null
                : new ApplicationIdentity(
                    ApplicationIdentityKind.CanonicalExecutablePath,
                    Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private delegate bool EnumWindowsProcedure(nint hwnd, nint parameter);
    private delegate void WinEventProcedure(nint hook, uint eventType, nint hwnd, int objectId,
        int childId, uint eventThread, uint eventTime);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowsProcedure callback, nint parameter);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint eventMin, uint eventMax,
        nint module, WinEventProcedure callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(nint hook);
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1838", Justification =
        "The callback performs one bounded title read; replacing this stable interop signature would add unsafe buffer ownership.")]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd,
        uint attribute, out int value, int valueSize);
}
