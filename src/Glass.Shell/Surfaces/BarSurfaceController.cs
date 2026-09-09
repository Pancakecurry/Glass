using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Shell;
using Glass.Platform.Windows.Windowing;
using Windows.Graphics;

namespace Glass.Shell.Surfaces;

public sealed partial class BarSurfaceController : IDisposable
{
    public const double SnapThreshold = 12;
    private const double RevealStrip = 2;
    private readonly WinUiWindowHandle _window;
    private readonly WindowsDisplayService _displays;
    private readonly NativeWindowMessageRouter _messages;
    private readonly AppBarController _appBar;
    private readonly IDisposable _enterSizeMoveRegistration;
    private readonly IDisposable _sizeMoveRegistration;
    private readonly IDisposable _dpiRegistration;
    private readonly IDisposable _taskbarCreatedRegistration;
    private readonly IDisposable _powerRegistration;
    private readonly IDisposable _sessionRegistration;
    private readonly uint _taskbarCreatedMessage;
    private bool _autoHidden;
    private bool _inUserMove;
    private bool _disposed;

    public BarSurfaceController(
        WinUiWindowHandle window,
        WindowsDisplayService displays,
        BarDefinition definition)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _displays = displays ?? throw new ArgumentNullException(nameof(displays));
        Definition = definition?.Normalize() ?? throw new ArgumentNullException(nameof(definition));
        _messages = new NativeWindowMessageRouter(window.Hwnd);
        _appBar = new AppBarController(window.Hwnd, _messages);
        _enterSizeMoveRegistration = _messages.Register(
            NativeWindowMessages.EnterSizeMove,
            OnEnterSizeMove);
        _sizeMoveRegistration = _messages.Register(
            NativeWindowMessages.ExitSizeMove,
            OnExitSizeMove);
        _dpiRegistration = _messages.Register(
            NativeWindowMessages.DpiChanged,
            OnDpiChanged);
        _taskbarCreatedMessage = NativeWindowMessages.TaskbarCreated;
        _taskbarCreatedRegistration = _messages.Register(
            _taskbarCreatedMessage,
            OnTaskbarCreated);
        _powerRegistration = _messages.Register(
            NativeWindowMessages.PowerBroadcast, OnPowerBroadcast);
        _sessionRegistration = _messages.Register(
            NativeWindowMessages.SessionChange, OnSessionChange);
        _ = RegisterSessionNotifications(_window.Hwnd, 0);
    }

    public BarDefinition Definition { get; private set; }

    public bool IsAppBarRegistered => _appBar.IsRegistered;

    public Exception? LastFailure { get; private set; }

    public event EventHandler<BarDefinitionChangedEventArgs>? DefinitionSettled;

    public event EventHandler? ShellRecovered;

    public event Action<SystemActivityState>? SystemActivityChanged;

    public void Apply(BarDefinition definition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Definition = definition.Normalize();
        if (Definition.Placement is FloatingPlacement)
        {
            _autoHidden = false;
        }
        ApplyCurrentPlacement();
    }

    public BarDefinition ReconcileDisplay()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var display = _displays.Resolve(Definition.Placement.Target);
        var target = WindowsDisplayService.ToTarget(display);
        Definition = Definition with
        {
            Placement = Retarget(Definition.Placement, target),
        };
        ApplyCurrentPlacement();
        return Definition;
    }

    public void SetAutoHidden(bool hidden)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_autoHidden == hidden)
        {
            return;
        }

        _autoHidden = hidden;
        ApplyCurrentPlacement();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = UnregisterSessionNotification(_window.Hwnd);
        _sessionRegistration.Dispose();
        _powerRegistration.Dispose();
        _taskbarCreatedRegistration.Dispose();
        _dpiRegistration.Dispose();
        _sizeMoveRegistration.Dispose();
        _enterSizeMoveRegistration.Dispose();
        _appBar.Dispose();
        _messages.Dispose();
        DefinitionSettled = null;
        ShellRecovered = null;
        SystemActivityChanged = null;
    }

    private void ApplyCurrentPlacement(uint? dpiOverride = null)
    {
        var display = _displays.Resolve(Definition.Placement.Target);
        var resolvedTarget = WindowsDisplayService.ToTarget(display);
        if (Definition.Placement.Target != resolvedTarget)
        {
            Definition = Definition with
            {
                Placement = Retarget(Definition.Placement, resolvedTarget),
            };
        }

        var dpi = dpiOverride ?? WindowPositioner.GetDpi(_window.Hwnd);
        ShellWindowStyle.Apply(_window.Hwnd, Definition.ZOrder);

        if (Definition.Placement is DockedPlacement docked && !_autoHidden)
        {
            var thickness = DpiConverter.ToPixels(docked.Thickness, dpi);
            try
            {
                _ = _appBar.Dock(display, docked.Edge, Math.Max(1, thickness));
                LastFailure = null;
                return;
            }
            catch (Exception exception)
            {
                LastFailure = exception;
                System.Diagnostics.Debug.WriteLine(
                    $"AppBar registration failed; using anchored fallback: {exception}");
                Definition = Definition with
                {
                    Placement = new AnchoredPlacement(
                        WindowsDisplayService.ToTarget(display),
                        docked.Edge,
                        0,
                        new LogicalSize(Definition.Length, Definition.Thickness)),
                };
            }
        }

        _appBar.Unregister();
        var logical = GetLogicalBounds(Definition, display, dpi);
        var native = DpiConverter.ToNative(logical, display.WorkArea, dpi);
        if (_autoHidden)
        {
            native = MoveToRevealStrip(native, display.Bounds, GetHideEdge(Definition.Placement));
        }

        WindowPositioner.MoveAndResize(_window.Hwnd, native);
    }

    private bool OnExitSizeMove(nuint wParam, nint lParam, out nint result)
    {
        _inUserMove = false;
        var display = _displays.GetForWindow(_window.WindowId);
        var dpi = WindowPositioner.GetDpi(_window.Hwnd);
        var nativeBounds = WindowPositioner.GetBounds(_window.Hwnd);
        var logicalBounds = DpiConverter.ToLogical(nativeBounds, display.WorkArea, dpi);
        var target = WindowsDisplayService.ToTarget(display);

        SurfacePlacement placement;
        if (Definition.Placement is DockedPlacement docked)
        {
            var thickness = docked.Edge is ScreenEdge.Top or ScreenEdge.Bottom
                ? logicalBounds.Height
                : logicalBounds.Width;
            placement = new DockedPlacement(target, docked.Edge, thickness);
        }
        else
        {
            var workArea = new LogicalRect(
                0,
                0,
                DpiConverter.ToLogical(display.WorkArea.Width, dpi),
                DpiConverter.ToLogical(display.WorkArea.Height, dpi));
            var snap = SnapEngine.Snap(logicalBounds, workArea, SnapThreshold);
            placement = snap.IsSnapped
                ? new AnchoredPlacement(
                    target,
                    snap.Edge!.Value,
                    snap.AlongEdgeOffset,
                    new LogicalSize(snap.Bounds.Width, snap.Bounds.Height))
                : new FloatingPlacement(target, snap.Bounds);
        }

        Definition = (Definition with
        {
            Placement = placement,
            Length = Definition.Orientation == BarOrientation.Horizontal
                ? logicalBounds.Width
                : logicalBounds.Height,
            Thickness = Definition.Orientation == BarOrientation.Horizontal
                ? logicalBounds.Height
                : logicalBounds.Width,
        }).Normalize();
        ApplyCurrentPlacement();
        DefinitionSettled?.Invoke(this, new BarDefinitionChangedEventArgs(Definition));
        result = 0;
        return false;
    }

    private bool OnDpiChanged(nuint wParam, nint lParam, out nint result)
    {
        var change = DpiChangedMessage.Parse(wParam, lParam);
        if (Definition.Placement is FloatingPlacement)
        {
            WindowPositioner.MoveAndResize(_window.Hwnd, change.SuggestedBounds);
            if (!_inUserMove)
            {
                var display = _displays.GetForWindow(_window.WindowId);
                var logical = DpiConverter.ToLogical(
                    change.SuggestedBounds,
                    display.WorkArea,
                    change.DpiX);
                Definition = (Definition with
                {
                    Placement = new FloatingPlacement(
                        WindowsDisplayService.ToTarget(display),
                        logical),
                    Length = Definition.Orientation == BarOrientation.Horizontal
                        ? logical.Width
                        : logical.Height,
                    Thickness = Definition.Orientation == BarOrientation.Horizontal
                        ? logical.Height
                        : logical.Width,
                }).Normalize();
                DefinitionSettled?.Invoke(this, new BarDefinitionChangedEventArgs(Definition));
            }
        }
        else
        {
            ApplyCurrentPlacement(change.DpiX);
        }

        result = 0;
        return true;
    }

    private bool OnTaskbarCreated(nuint wParam, nint lParam, out nint result)
    {
        if (Definition.Placement is DockedPlacement && !_autoHidden)
            _appBar.RecoverAfterShellRestart();
        else
            ApplyCurrentPlacement();
        ShellRecovered?.Invoke(this, EventArgs.Empty);
        result = 0;
        return true;
    }

    private bool OnPowerBroadcast(nuint wParam, nint lParam, out nint result)
    {
        const nuint suspend = 0x0004;
        const nuint resumeAutomatic = 0x0012;
        if (wParam == suspend)
            SystemActivityChanged?.Invoke(new(false, "suspend"));
        else if (wParam == resumeAutomatic)
        {
            ApplyCurrentPlacement();
            SystemActivityChanged?.Invoke(new(true, "resume"));
        }
        result = 0;
        return false;
    }

    private bool OnSessionChange(nuint wParam, nint lParam, out nint result)
    {
        const nuint lockSession = 0x0007;
        const nuint unlockSession = 0x0008;
        if (wParam == lockSession)
            SystemActivityChanged?.Invoke(new(false, "lock"));
        else if (wParam == unlockSession)
        {
            ApplyCurrentPlacement();
            SystemActivityChanged?.Invoke(new(true, "unlock"));
        }
        result = 0;
        return false;
    }

    [System.Runtime.InteropServices.LibraryImport("wtsapi32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool RegisterSessionNotifications(nint window, uint flags);

    [System.Runtime.InteropServices.LibraryImport("wtsapi32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool UnregisterSessionNotification(nint window);

    private bool OnEnterSizeMove(nuint wParam, nint lParam, out nint result)
    {
        _inUserMove = true;
        result = 0;
        return false;
    }

    private static LogicalRect GetLogicalBounds(
        BarDefinition definition,
        DisplayInfo display,
        uint dpi)
    {
        var work = new LogicalRect(
            0,
            0,
            DpiConverter.ToLogical(display.WorkArea.Width, dpi),
            DpiConverter.ToLogical(display.WorkArea.Height, dpi));
        if (definition.Placement is FloatingPlacement floating)
        {
            return floating.Bounds.ClampInside(work);
        }

        var edge = definition.Placement switch
        {
            AnchoredPlacement anchored => anchored.Edge,
            DockedPlacement docked => docked.Edge,
            _ => throw new InvalidOperationException("Unknown surface placement."),
        };
        var offset = definition.Placement is AnchoredPlacement value
            ? value.AlongEdgeOffset
            : 0;
        var availableLength = definition.Orientation == BarOrientation.Horizontal
            ? work.Width
            : work.Height;
        var length = definition.LengthMode == BarLengthMode.Fill
            ? availableLength
            : Math.Min(definition.Length, availableLength);
        var width = definition.Orientation == BarOrientation.Horizontal
            ? length
            : definition.Thickness;
        var height = definition.Orientation == BarOrientation.Horizontal
            ? definition.Thickness
            : length;
        var x = work.X + ((work.Width - width) / 2) +
            (edge is ScreenEdge.Top or ScreenEdge.Bottom ? offset : 0);
        var y = work.Y + ((work.Height - height) / 2) +
            (edge is ScreenEdge.Left or ScreenEdge.Right ? offset : 0);
        x = edge switch
        {
            ScreenEdge.Left => work.X,
            ScreenEdge.Right => work.Right - width,
            _ => x,
        };
        y = edge switch
        {
            ScreenEdge.Top => work.Y,
            ScreenEdge.Bottom => work.Bottom - height,
            _ => y,
        };
        return new LogicalRect(x, y, width, height).ClampInside(work);
    }

    private static RectInt32 MoveToRevealStrip(
        RectInt32 bounds,
        RectInt32 display,
        ScreenEdge edge) =>
        edge switch
        {
            ScreenEdge.Top => new RectInt32(
                bounds.X, display.Y - bounds.Height + (int)RevealStrip, bounds.Width, bounds.Height),
            ScreenEdge.Bottom => new RectInt32(
                bounds.X, display.Y + display.Height - (int)RevealStrip, bounds.Width, bounds.Height),
            ScreenEdge.Left => new RectInt32(
                display.X - bounds.Width + (int)RevealStrip, bounds.Y, bounds.Width, bounds.Height),
            ScreenEdge.Right => new RectInt32(
                display.X + display.Width - (int)RevealStrip, bounds.Y, bounds.Width, bounds.Height),
            _ => bounds,
        };

    private static ScreenEdge GetHideEdge(SurfacePlacement placement) =>
        placement switch
        {
            AnchoredPlacement anchored => anchored.Edge,
            DockedPlacement docked => docked.Edge,
            _ => ScreenEdge.Bottom,
        };

    private static SurfacePlacement Retarget(
        SurfacePlacement placement,
        DisplayTarget target) =>
        placement switch
        {
            FloatingPlacement floating => floating with { Target = target },
            AnchoredPlacement anchored => anchored with { Target = target },
            DockedPlacement docked => docked with { Target = target },
            _ => throw new InvalidOperationException("Unknown surface placement."),
        };
}
