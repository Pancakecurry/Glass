using Glass.App.Configuration;
using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Glass.Shell.Surfaces;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Glass.App;

public sealed partial class BarWindow : Window, IBarSurface
{
    private readonly WinUiWindowHandle _nativeWindow;
    private readonly BarSurfaceController _surfaceController;
    private readonly AutoHideController _autoHide = new();
    private bool _disposed;

    public BarWindow(WindowsDisplayService displays, BarDefinition definition)
    {
        InitializeComponent();
        Title = ProductBranding.BarWindowTitle;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);

        _nativeWindow = WinUiWindowHandle.FromWindow(this);
        if (_nativeWindow.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _surfaceController = new BarSurfaceController(_nativeWindow, displays, definition);
        _surfaceController.DefinitionSettled += OnDefinitionSettled;
        _autoHide.StateChanged += OnAutoHideStateChanged;
        Apply(definition);
        ApplyBackdrop();
        Closed += OnClosed;
    }

    public BarId Id => Definition.Id;

    public BarDefinition Definition => _surfaceController.Definition;

    public bool IsVisible { get; private set; }

    public Exception? LastFailure => _surfaceController.LastFailure;

    public event EventHandler<BarDefinitionChangedEventArgs>? DefinitionSettled;

    public void Apply(BarDefinition definition)
    {
        _surfaceController.Apply(definition);
        _autoHide.SetEnabled(definition.AutoHideEnabled);
        ZonePanel.Orientation = definition.Orientation == BarOrientation.Horizontal
            ? Orientation.Horizontal
            : Orientation.Vertical;
    }

    public BarDefinition ReconcileDisplay() => _surfaceController.ReconcileDisplay();

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsVisible = visible;
        if (visible)
        {
            _nativeWindow.AppWindow.Show(false);
        }
        else
        {
            WindowPositioner.Hide(_nativeWindow.Hwnd);
        }
    }

    public new void Close()
    {
        if (!_disposed)
        {
            base.Close();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Closed -= OnClosed;
        _autoHide.StateChanged -= OnAutoHideStateChanged;
        _surfaceController.DefinitionSettled -= OnDefinitionSettled;
        _autoHide.Dispose();
        _surfaceController.Dispose();
        DefinitionSettled = null;
    }

    private void ApplyBackdrop()
    {
        try
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch
        {
            Root.Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 32, 32, 32));
        }
    }

    private void Root_PointerEntered(object sender, PointerRoutedEventArgs args) =>
        _autoHide.PointerEntered();

    private void Root_PointerExited(object sender, PointerRoutedEventArgs args) =>
        _autoHide.PointerExited();

    private void OnAutoHideStateChanged(AutoHideState state) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                _surfaceController.SetAutoHidden(state == AutoHideState.Hidden);
            }
        });

    private void OnDefinitionSettled(
        object? sender,
        BarDefinitionChangedEventArgs args) =>
        DefinitionSettled?.Invoke(this, args);

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();
}
