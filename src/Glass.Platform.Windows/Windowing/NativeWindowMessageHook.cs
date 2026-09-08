using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glass.Platform.Windows.Windowing;

public sealed class NativeWindowMessageEventArgs : EventArgs
{
    internal NativeWindowMessageEventArgs(uint message, nuint wParam, nint lParam)
    {
        Message = message;
        WParam = wParam;
        LParam = lParam;
    }

    public uint Message { get; }

    public nuint WParam { get; }

    public nint LParam { get; }

    public bool Handled { get; set; }

    public nint Result { get; set; }
}

public sealed class NativeWindowMessageHook : IDisposable
{
    private static long _nextSubclassId;

    private readonly nint _hwnd;
    private readonly nuint _subclassId;
    private readonly SubclassProcedure _subclassProcedure;
    private bool _disposed;

    public NativeWindowMessageHook(nint hwnd)
    {
        if (hwnd == 0)
        {
            throw new ArgumentException("A valid HWND is required.", nameof(hwnd));
        }

        _hwnd = hwnd;
        _subclassId = unchecked((nuint)Interlocked.Increment(ref _nextSubclassId));
        _subclassProcedure = WindowSubclassProcedure;

        if (!SetWindowSubclass(_hwnd, _subclassProcedure, _subclassId, 0))
        {
            throw new InvalidOperationException("SetWindowSubclass failed for the WinUI window.");
        }
    }

    public event EventHandler<NativeWindowMessageEventArgs>? MessageReceived;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        MessageReceived = null;
        _ = RemoveWindowSubclass(_hwnd, _subclassProcedure, _subclassId);
    }

    private nint WindowSubclassProcedure(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        try
        {
            var args = new NativeWindowMessageEventArgs(message, wParam, lParam);
            MessageReceived?.Invoke(this, args);
            if (args.Handled)
            {
                return args.Result;
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Native message callback failed: {exception}");
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProcedure(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        nint hwnd,
        SubclassProcedure subclassProcedure,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        nint hwnd,
        SubclassProcedure subclassProcedure,
        nuint subclassId);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam);
}
