using Glass.App.Configuration;
using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Glass.Platform.Windows.Applications;
using Glass.Core.Applications;
using Glass.Shell.Surfaces;
using Glass.Shell.Runtime;
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
    private readonly RunningWindowTracker _runningWindows;
    private readonly ApplicationLaunchService _launcher;
    private bool _disposed;

    public BarWindow(
        WindowsDisplayService displays,
        RunningWindowTracker runningWindows,
        ApplicationLaunchService launcher,
        BarDefinition definition)
    {
        InitializeComponent();
        Title = ProductBranding.BarWindowTitle;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        _runningWindows = runningWindows;
        _launcher = launcher;

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
        _runningWindows.Changed += OnRunningWindowsChanged;
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
        definition = definition.Normalize();
        _autoHide.SetEnabled(definition.AutoHideEnabled);
        _surfaceController.Apply(definition);
        ZonePanel.Orientation = definition.Orientation == BarOrientation.Horizontal
            ? Orientation.Horizontal
            : Orientation.Vertical;
        ContentPanel.Orientation = ZonePanel.Orientation;
        RenderContent();
    }

    public ValueTask<BarDefinition> ReconcileDisplayAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (DispatcherQueue.HasThreadAccess)
        {
            return ValueTask.FromResult(_surfaceController.ReconcileDisplay());
        }

        var completion = new TaskCompletionSource<BarDefinition>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    completion.SetResult(_surfaceController.ReconcileDisplay());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException(
                "The bar window dispatcher is no longer available."));
        }

        return new ValueTask<BarDefinition>(completion.Task);
    }

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
        _runningWindows.Changed -= OnRunningWindowsChanged;
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

    private void OnRunningWindowsChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(RenderContent);

    private void RenderContent()
    {
        if (_disposed) return;
        ContentPanel.Children.Clear();
        foreach (var item in Definition.Content)
        {
            switch (item)
            {
                case PinnedApplicationBarItem application:
                    AddApplicationButton(application.Application,
                        _runningWindows.Current.FirstOrDefault(group =>
                            group.Identity == application.Application));
                    break;
                case RunningApplicationsSlotBarItem:
                    foreach (var group in TaskbarProjection.UnpinnedRunningApplications(
                        Definition, _runningWindows.Current))
                        AddApplicationButton(group.Identity, group);
                    break;
                case WidgetBarItem widget:
                    ContentPanel.Children.Add(new Button
                    {
                        Content = $"Widget {widget.WidgetInstanceId.ToString()[..8]}",
                        Tag = widget.WidgetInstanceId,
                    });
                    break;
                case SpacerBarItem spacer:
                    ContentPanel.Children.Add(new Border
                    {
                        Width = Definition.Orientation == BarOrientation.Horizontal
                            ? (spacer.IsFlexible ? 24 : spacer.LogicalSize) : 1,
                        Height = Definition.Orientation == BarOrientation.Vertical
                            ? (spacer.IsFlexible ? 24 : spacer.LogicalSize) : 1,
                    });
                    break;
            }
        }
    }

    private void AddApplicationButton(
        ApplicationIdentity identity,
        RunningApplicationGroup? group)
    {
        var button = new Button
        {
            Content = group?.DisplayName ?? identity.Value,
            Tag = identity,
            Opacity = group is null ? 0.72 : 1,
            FontWeight = group?.IsActive == true
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal,
        };
        button.Click += (_, _) =>
        {
            if (group is null) _launcher.Launch(identity);
            else if (group.Windows.Count == 1) _launcher.ActivateOrToggle(group.Windows[0]);
            else ShowWindowChooser(button, group);
        };
        ContentPanel.Children.Add(button);
    }

    private void ShowWindowChooser(Button button, RunningApplicationGroup group)
    {
        var flyout = new MenuFlyout();
        foreach (var window in group.Windows)
        {
            var item = new MenuFlyoutItem { Text = window.Title };
            item.Click += (_, _) => _launcher.ActivateOrToggle(window);
            flyout.Items.Add(item);
        }
        flyout.ShowAt(button);
    }

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();
}
