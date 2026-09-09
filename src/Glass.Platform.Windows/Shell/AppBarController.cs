using System.ComponentModel;
using System.Runtime.InteropServices;
using Glass.Core.Placement;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Windows.Graphics;

namespace Glass.Platform.Windows.Shell;

public sealed partial class AppBarController : IDisposable
{
    private const uint AbmNew = 0x00000000;
    private const uint AbmRemove = 0x00000001;
    private const uint AbmQueryPos = 0x00000002;
    private const uint AbmSetPos = 0x00000003;
    private const uint AbnPosChanged = 0x00000001;

    private readonly nint _hwnd;
    private readonly IDisposable _callbackRegistration;
    private readonly uint _callbackMessage;
    private DockRequest? _lastRequest;
    private bool _registered;
    private bool _positioning;
    private bool _disposed;

    public AppBarController(nint hwnd, NativeWindowMessageRouter messageRouter)
    {
        if (hwnd == 0)
        {
            throw new ArgumentException("A valid HWND is required.", nameof(hwnd));
        }

        _hwnd = hwnd;
        ArgumentNullException.ThrowIfNull(messageRouter);
        _callbackMessage = RegisterWindowMessage(
            $"Glass.AppBar.Callback.{Environment.ProcessId}.{hwnd:X}");
        if (_callbackMessage == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _callbackRegistration = messageRouter.Register(_callbackMessage, OnAppBarMessage);
    }

    public bool IsRegistered => _registered;

    public event EventHandler? RegistrationChanged;

    public RectInt32 Dock(DisplayInfo display, ScreenEdge edge, int thicknessPixels)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(display);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thicknessPixels);

        Register();
        _lastRequest = new DockRequest(display, edge, thicknessPixels);
        var previousBounds = WindowPositioner.GetBounds(_hwnd);

        try
        {
            return ApplyPosition(_lastRequest);
        }
        catch
        {
            Unregister();
            WindowPositioner.MoveAndResize(_hwnd, previousBounds);
            throw;
        }
    }

    public void Unregister()
    {
        if (!_registered)
        {
            _lastRequest = null;
            return;
        }

        var data = CreateData();
        _ = SHAppBarMessage(AbmRemove, ref data);
        _registered = false;
        _lastRequest = null;
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _disposed = true;
        _callbackRegistration.Dispose();
        RegistrationChanged = null;
    }

    private void Register()
    {
        if (_registered)
        {
            return;
        }

        var data = CreateData();
        data.CallbackMessage = _callbackMessage;

        if (SHAppBarMessage(AbmNew, ref data) == 0)
        {
            throw new InvalidOperationException("Windows rejected AppBar registration.");
        }

        _registered = true;
        RegistrationChanged?.Invoke(this, EventArgs.Empty);
    }

    private RectInt32 ApplyPosition(DockRequest request)
    {
        if (_positioning)
        {
            return request.Display.Bounds;
        }

        _positioning = true;
        try
        {
            var data = CreateData();
            data.Edge = ToNativeEdge(request.Edge);
            data.Rectangle = NativeRect.From(request.Display.Bounds);

            _ = SHAppBarMessage(AbmQueryPos, ref data);
            data.Rectangle = ConstrainThickness(
                data.Rectangle,
                request.Edge,
                request.ThicknessPixels);
            _ = SHAppBarMessage(AbmSetPos, ref data);

            var finalBounds = data.Rectangle.ToRectInt32();
            WindowPositioner.MoveAndResize(_hwnd, finalBounds);
            return finalBounds;
        }
        finally
        {
            _positioning = false;
        }
    }

    public void Reapply(DisplayInfo display)
    {
        if (_lastRequest is null)
        {
            return;
        }

        _lastRequest = _lastRequest with { Display = display };
        _ = ApplyPosition(_lastRequest);
    }

    public void RecoverAfterShellRestart()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var request = _lastRequest;
        if (request is null) return;
        _registered = false;
        Register();
        _lastRequest = request;
        _ = ApplyPosition(request);
    }

    private bool OnAppBarMessage(nuint wParam, nint lParam, out nint result)
    {
        if ((uint)wParam == AbnPosChanged &&
            _registered &&
            _lastRequest is not null &&
            !_positioning)
        {
            _ = ApplyPosition(_lastRequest);
        }

        result = 0;
        return true;
    }

    private AppBarData CreateData() =>
        new()
        {
            Size = (uint)Marshal.SizeOf<AppBarData>(),
            Hwnd = _hwnd,
        };

    private static NativeRect ConstrainThickness(
        NativeRect rectangle,
        ScreenEdge edge,
        int thickness)
    {
        switch (edge)
        {
            case ScreenEdge.Left:
                rectangle.Right = checked(rectangle.Left + thickness);
                break;
            case ScreenEdge.Right:
                rectangle.Left = checked(rectangle.Right - thickness);
                break;
            case ScreenEdge.Top:
                rectangle.Bottom = checked(rectangle.Top + thickness);
                break;
            case ScreenEdge.Bottom:
                rectangle.Top = checked(rectangle.Bottom - thickness);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(edge));
        }

        return rectangle;
    }

    private static uint ToNativeEdge(ScreenEdge edge) =>
        edge switch
        {
            ScreenEdge.Left => 0,
            ScreenEdge.Top => 1,
            ScreenEdge.Right => 2,
            ScreenEdge.Bottom => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };

    [LibraryImport("shell32.dll")]
    private static partial nuint SHAppBarMessage(
        uint message,
        ref AppBarData data);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW",
        StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint RegisterWindowMessage(string message);

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public nint Hwnd;
        public uint CallbackMessage;
        public uint Edge;
        public NativeRect Rectangle;
        public nint Parameter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public static NativeRect From(RectInt32 rectangle) =>
            new(
                rectangle.X,
                rectangle.Y,
                checked(rectangle.X + rectangle.Width),
                checked(rectangle.Y + rectangle.Height));

        public RectInt32 ToRectInt32() =>
            new(Left, Top, checked(Right - Left), checked(Bottom - Top));
    }

    private sealed record DockRequest(
        DisplayInfo Display,
        ScreenEdge Edge,
        int ThicknessPixels);
}
