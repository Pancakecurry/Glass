using Glass.Core.Placement;
using Glass.App.Configuration;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Media;
using Glass.Platform.Windows.Windowing;
using Glass.Rendering.Composition;
using Glass.Shell;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Glass.App;

public sealed partial class ProbeBarWindow : Window, IDisposable
{
    private readonly WinUiWindowHandle _nativeWindow;
    private readonly ProbeSurfaceCoordinator _coordinator;
    private bool _programmaticEmphasis;
    private bool _disposed;

    public ProbeBarWindow()
    {
        InitializeComponent();
        Title = ProductBranding.TechnicalProbeWindowTitle;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);

        _nativeWindow = WinUiWindowHandle.FromWindow(this);
        if (_nativeWindow.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _coordinator = new ProbeSurfaceCoordinator(_nativeWindow);
        _coordinator.StateChanged += OnCoordinatorStateChanged;
        BackdropStatus = ApplyBackdrop();
        Closed += OnClosed;
    }

    public nint Hwnd => _nativeWindow.Hwnd;

    public WinUiWindowHandle NativeWindow => _nativeWindow;

    public SurfacePlacement Placement => _coordinator.Placement;

    public bool IsAppBarRegistered => _coordinator.IsAppBarRegistered;

    public string BackdropStatus { get; }

    public event EventHandler? StateChanged;

    public void ShowSurface()
    {
        Activate();
        WindowPositioner.Show(Hwnd);
    }

    public void HideSurface() => WindowPositioner.Hide(Hwnd);

    public void CenterOn(DisplayInfo display) => _coordinator.CenterOn(display);

    public void Dock(DisplayInfo display, DockEdge edge) =>
        _coordinator.Dock(display, edge);

    public void Undock(DisplayInfo display) => _coordinator.Undock(display);

    public void TriggerAnimation()
    {
        _programmaticEmphasis = !_programmaticEmphasis;
        CompositionAnimator.AnimateEmphasis(ProbeItemA, _programmaticEmphasis);
    }

    public void UpdateMedia(MediaSessionSnapshot snapshot)
    {
        MediaTitleText.Text = snapshot.IsAvailable
            ? string.Join(" — ", new[] { snapshot.Title, snapshot.Artist }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            : snapshot.Status;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Closed -= OnClosed;
        _coordinator.StateChanged -= OnCoordinatorStateChanged;
        _coordinator.Dispose();
        StateChanged = null;
    }

    private string ApplyBackdrop()
    {
        try
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
            return "Desktop Acrylic requested; fallback is managed by Windows";
        }
        catch (Exception exception)
        {
            Root.Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 32, 32, 32));
            return $"Solid fallback active: {exception.Message}";
        }
    }

    private void ProbeItem_Loaded(object sender, RoutedEventArgs args)
    {
        if (sender is UIElement element)
        {
            CompositionAnimator.PrepareScale(element);
        }
    }

    private void ProbeItem_PointerEntered(
        object sender,
        PointerRoutedEventArgs args)
    {
        if (sender is UIElement element)
        {
            CompositionAnimator.AnimateEmphasis(element, true);
        }
    }

    private void ProbeItem_PointerExited(
        object sender,
        PointerRoutedEventArgs args)
    {
        if (sender is UIElement element)
        {
            CompositionAnimator.AnimateEmphasis(element, false);
        }
    }

    private void OnCoordinatorStateChanged(object? sender, EventArgs args) =>
        StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();
}
