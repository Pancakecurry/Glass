using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Glass.App;

public sealed partial class WidgetWindow : Window, IDisposable
{
    private readonly WindowsDisplayService _displays;
    private readonly WinUiWindowHandle _native;
    private readonly NativeWindowMessageRouter _messages;
    private readonly IDisposable _moveRegistration;
    private readonly IDisposable _dpiRegistration;
    private bool _disposed;

    public WidgetWindow(WindowsDisplayService displays,
        StandaloneWidgetDefinition surface, WidgetInstanceDefinition instance)
    {
        InitializeComponent();
        _displays = displays;
        Surface = surface;
        Instance = instance;
        TitleText.Text = instance.WidgetTypeId;
        StatusText.Text = "Functional widget host — Phase 3 owns final presentation.";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        _native = WinUiWindowHandle.FromWindow(this);
        if (_native.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
        }
        ShellWindowStyle.Apply(_native.Hwnd, surface.ZOrder);
        _messages = new NativeWindowMessageRouter(_native.Hwnd);
        _moveRegistration = _messages.Register(NativeWindowMessages.ExitSizeMove, OnSettled);
        _dpiRegistration = _messages.Register(NativeWindowMessages.DpiChanged, OnDpiChanged);
        ApplyPlacement(surface.Placement, instance.Size);
        Closed += OnClosed;
    }

    public StandaloneWidgetDefinition Surface { get; private set; }
    public WidgetInstanceDefinition Instance { get; }
    public event Action<StandaloneWidgetDefinition>? DefinitionSettled;

    public void Present() => _native.AppWindow.Show(false);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Closed -= OnClosed;
        _dpiRegistration.Dispose();
        _moveRegistration.Dispose();
        _messages.Dispose();
        DefinitionSettled = null;
    }

    private void ApplyPlacement(SurfacePlacement placement, LogicalSize size)
    {
        var display = _displays.Resolve(placement.Target);
        var dpi = WindowPositioner.GetDpi(_native.Hwnd);
        LogicalRect logical = placement is FloatingPlacement floating
            ? floating.Bounds
            : new LogicalRect(100, 100, size.Width, size.Height);
        WindowPositioner.MoveAndResize(_native.Hwnd,
            DpiConverter.ToNative(logical, display.WorkArea, dpi));
    }

    private bool OnDpiChanged(nuint wParam, nint lParam, out nint result)
    {
        var change = DpiChangedMessage.Parse(wParam, lParam);
        WindowPositioner.MoveAndResize(_native.Hwnd, change.SuggestedBounds);
        Capture(change.DpiX, change.SuggestedBounds);
        result = 0;
        return true;
    }

    private bool OnSettled(nuint wParam, nint lParam, out nint result)
    {
        Capture(WindowPositioner.GetDpi(_native.Hwnd), WindowPositioner.GetBounds(_native.Hwnd));
        result = 0;
        return false;
    }

    private void Capture(uint dpi, Windows.Graphics.RectInt32 bounds)
    {
        var display = _displays.GetForWindow(_native.WindowId);
        var logical = DpiConverter.ToLogical(bounds, display.WorkArea, dpi);
        Surface = Surface with
        {
            Placement = new FloatingPlacement(WindowsDisplayService.ToTarget(display), logical),
            Size = new LogicalSize(logical.Width, logical.Height),
        };
        DefinitionSettled?.Invoke(Surface);
    }

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();
}
