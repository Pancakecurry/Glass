using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glass.Platform.Windows.Windowing;

public delegate bool NativeMessageHandler(nuint wParam, nint lParam, out nint result);

public static class NativeWindowMessages
{
    public const uint EnterSizeMove = 0x0231;
    public const uint ExitSizeMove = 0x0232;
    public const uint DpiChanged = 0x02E0;
}

public sealed class NativeWindowMessageRouter : IDisposable
{
    private static long _nextSubclassId;
    private readonly nint _hwnd;
    private readonly nuint _subclassId;
    private readonly SubclassProcedure _subclassProcedure;
    private readonly Dictionary<uint, HandlerRegistration[]> _handlers = [];
    private bool _disposed;

    public NativeWindowMessageRouter(nint hwnd)
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

    public IDisposable Register(uint message, NativeMessageHandler handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(handler);

        var registration = new HandlerRegistration(this, message, handler);
        _handlers.TryGetValue(message, out var current);
        current ??= [];
        var updated = new HandlerRegistration[current.Length + 1];
        Array.Copy(current, updated, current.Length);
        updated[^1] = registration;
        _handlers[message] = updated;
        return registration;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _handlers.Clear();
        _ = RemoveWindowSubclass(_hwnd, _subclassProcedure, _subclassId);
    }

    private void Unregister(HandlerRegistration registration)
    {
        if (_disposed || !_handlers.TryGetValue(registration.Message, out var current))
        {
            return;
        }

        var index = Array.IndexOf(current, registration);
        if (index < 0)
        {
            return;
        }

        if (current.Length == 1)
        {
            _handlers.Remove(registration.Message);
            return;
        }

        var updated = new HandlerRegistration[current.Length - 1];
        if (index > 0)
        {
            Array.Copy(current, 0, updated, 0, index);
        }

        if (index < current.Length - 1)
        {
            Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
        }

        _handlers[registration.Message] = updated;
    }

    private nint WindowSubclassProcedure(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        if (!_handlers.TryGetValue(message, out var registrations))
        {
            return DefSubclassProc(hwnd, message, wParam, lParam);
        }

        foreach (var registration in registrations)
        {
            try
            {
                if (registration.Handler(wParam, lParam, out var result))
                {
                    return result;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Native message handler failed for 0x{message:X}: {exception}");
            }
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private sealed class HandlerRegistration : IDisposable
    {
        private NativeWindowMessageRouter? _owner;

        public HandlerRegistration(
            NativeWindowMessageRouter owner,
            uint message,
            NativeMessageHandler handler)
        {
            _owner = owner;
            Message = message;
            Handler = handler;
        }

        public uint Message { get; }

        public NativeMessageHandler Handler { get; }

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unregister(this);
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
