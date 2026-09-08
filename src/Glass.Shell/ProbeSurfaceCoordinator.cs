using Glass.Core.Placement;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Shell;
using Glass.Platform.Windows.Windowing;
using Windows.Graphics;

namespace Glass.Shell;

public sealed class ProbeSurfaceCoordinator : IDisposable
{
    private const double FloatingWidthDips = 520;
    private const double FloatingHeightDips = 72;
    private const double DockThicknessDips = 72;

    private readonly WinUiWindowHandle _window;
    private readonly AppBarController _appBar;
    private bool _disposed;

    public ProbeSurfaceCoordinator(WinUiWindowHandle window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _appBar = new AppBarController(window.Hwnd);
        _appBar.RegistrationChanged += OnRegistrationChanged;
    }

    public SurfacePlacement Placement { get; private set; } =
        SurfacePlacement.Floating;

    public bool IsAppBarRegistered => _appBar.IsRegistered;

    public event EventHandler? StateChanged;

    public RectInt32 CenterOn(DisplayInfo display)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(display);

        _appBar.Unregister();
        var dpi = WindowPositioner.GetDpi(_window.Hwnd);
        var width = WindowPositioner.DipsToPixels(FloatingWidthDips, dpi);
        var height = WindowPositioner.DipsToPixels(FloatingHeightDips, dpi);
        var bounds = WindowPositioner.CenterIn(display.WorkArea, width, height);
        WindowPositioner.MoveAndResize(_window.Hwnd, bounds);
        Placement = SurfacePlacement.Floating;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return bounds;
    }

    public RectInt32 Dock(DisplayInfo display, DockEdge edge)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(display);

        // Moving first lets GetDpiForWindow observe the target monitor before
        // converting the logical dock thickness to native screen pixels.
        CenterOn(display);
        var thickness = WindowPositioner.DipsToPixels(
            DockThicknessDips,
            WindowPositioner.GetDpi(_window.Hwnd));

        try
        {
            var bounds = _appBar.Dock(display, edge, thickness);
            Placement = SurfacePlacement.Docked(edge);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return bounds;
        }
        catch
        {
            Placement = SurfacePlacement.Floating;
            StateChanged?.Invoke(this, EventArgs.Empty);
            throw;
        }
    }

    public RectInt32 Undock(DisplayInfo display) => CenterOn(display);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _appBar.RegistrationChanged -= OnRegistrationChanged;
        _appBar.Dispose();
        StateChanged = null;
    }

    private void OnRegistrationChanged(object? sender, EventArgs args) =>
        StateChanged?.Invoke(this, EventArgs.Empty);
}
