using Glass.App.Configuration;
using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Shell;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Glass.Shell.Surfaces;
using Glass.Widgets.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace Glass.App;

public sealed partial class BarWindow : Window, IBarSurface
{
    private readonly WinUiWindowHandle _nativeWindow;
    private readonly BarSurfaceController _surfaceController;
    private readonly AutoHideController _autoHide = new();
    private readonly RunningWindowTracker _runningWindows;
    private readonly ApplicationLaunchService _launcher;
    private readonly ProductSurfaceServices _services;
    private readonly List<FrameworkElement> _applicationElements = [];
    private bool _disposed;

    internal BarWindow(
        WindowsDisplayService displays,
        RunningWindowTracker runningWindows,
        ApplicationLaunchService launcher,
        ProductSurfaceServices services,
        BarDefinition definition)
    {
        InitializeComponent();
        Title = ProductBranding.BarWindowTitle;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        _runningWindows = runningWindows;
        _launcher = launcher;
        _services = services;

        _nativeWindow = WinUiWindowHandle.FromWindow(this);
        _runningWindows.RegisterGlassWindow(_nativeWindow.Hwnd);
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
        _services.EditMode.Changed += OnEditModeChanged;
        Apply(definition);
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
        EditLabel.Text = $"{definition.Name} · Bar";
        _autoHide.SetEnabled(definition.AutoHideEnabled);
        _surfaceController.Apply(definition);
        ApplyOrientation(definition.Orientation);
        ApplyAppearance();
        RenderContent();
        SetHostedWidgetsVisible(IsVisible && _autoHide.State != AutoHideState.Hidden);
    }

    public ValueTask<BarDefinition> ReconcileDisplayAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (DispatcherQueue.HasThreadAccess)
            return ValueTask.FromResult(_surfaceController.ReconcileDisplay());
        var completion = new TaskCompletionSource<BarDefinition>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            try { completion.SetResult(_surfaceController.ReconcileDisplay()); }
            catch (Exception exception) { completion.SetException(exception); }
        }))
            completion.SetException(new InvalidOperationException(
                "The bar window dispatcher is no longer available."));
        return new ValueTask<BarDefinition>(completion.Task);
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsVisible = visible;
        if (visible) _nativeWindow.AppWindow.Show(false);
        else WindowPositioner.Hide(_nativeWindow.Hwnd);
        SetHostedWidgetsVisible(visible);
    }

    public new void Close()
    {
        if (!_disposed) base.Close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Closed -= OnClosed;
        _autoHide.StateChanged -= OnAutoHideStateChanged;
        _runningWindows.Changed -= OnRunningWindowsChanged;
        _surfaceController.DefinitionSettled -= OnDefinitionSettled;
        _services.EditMode.Changed -= OnEditModeChanged;
        SetHostedWidgetsVisible(false);
        _autoHide.Dispose();
        _surfaceController.Dispose();
        DefinitionSettled = null;
    }

    private void ApplyOrientation(BarOrientation orientation)
    {
        var horizontal = orientation == BarOrientation.Horizontal;
        foreach (var panel in Panels()) panel.Orientation = horizontal
            ? Orientation.Horizontal : Orientation.Vertical;
        SurfaceLayout.ColumnDefinitions.Clear();
        SurfaceLayout.RowDefinitions.Clear();
        if (horizontal)
        {
            SurfaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            SurfaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            SurfaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            SetZonePosition(StartIsland, 0, 0);
            SetZonePosition(CenterIsland, 1, 0);
            SetZonePosition(EndIsland, 2, 0);
            CenterIsland.HorizontalAlignment = HorizontalAlignment.Center;
            CenterIsland.VerticalAlignment = VerticalAlignment.Stretch;
        }
        else
        {
            SurfaceLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SurfaceLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            SurfaceLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SetZonePosition(StartIsland, 0, 0);
            SetZonePosition(CenterIsland, 0, 1);
            SetZonePosition(EndIsland, 0, 2);
            CenterIsland.HorizontalAlignment = HorizontalAlignment.Stretch;
            CenterIsland.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    private void ApplyAppearance()
    {
        var settings = _services.Settings().Normalize();
        var material = AppearanceResolver.ForBar(settings, Id.Value);
        _services.Materials.Apply(this, SurfaceChrome, material,
            settings.Appearance.ThemeMode, Glass.Rendering.Materials.GlassSurfaceRole.Bar);
        SurfaceChrome.Padding = new Thickness(settings.Appearance.BarPadding);
        foreach (var panel in Panels()) panel.Spacing = settings.Appearance.ItemSpacing;
        var separated = Definition.VisualMode == BarVisualMode.Segmented;
        var minimal = Definition.VisualMode == BarVisualMode.Minimal;
        foreach (var island in Islands())
        {
            island.CornerRadius = new CornerRadius(Math.Max(8, material.CornerRadius - 4));
            island.Background = separated
                ? new SolidColorBrush(Color.FromArgb(24, 255, 255, 255))
                : new SolidColorBrush(Colors.Transparent);
            island.BorderThickness = separated ? new Thickness(1) : new Thickness(0);
            island.BorderBrush = Glass.Rendering.Materials.GlassMaterialController.EdgeHighlight(material);
        }
        SurfaceChrome.Background = minimal ? new SolidColorBrush(Colors.Transparent) : SurfaceChrome.Background;
        EditOutline.CornerRadius = new CornerRadius(material.CornerRadius + 3);
        OnEditModeChanged(this, EventArgs.Empty);
    }

    private void RenderContent()
    {
        if (_disposed) return;
        foreach (var panel in Panels()) panel.Children.Clear();
        _applicationElements.Clear();
        foreach (var item in Definition.Content)
        {
            var panel = PanelFor(item.Zone);
            switch (item)
            {
                case PinnedApplicationBarItem application:
                    AddApplicationItem(panel, application.Application,
                        _runningWindows.Current.FirstOrDefault(group =>
                            group.Identity == application.Application), isPinned: true);
                    break;
                case RunningApplicationsSlotBarItem:
                    foreach (var group in TaskbarProjection.UnpinnedRunningApplications(
                        Definition, _runningWindows.Current))
                        AddApplicationItem(panel, group.Identity, group, isPinned: false);
                    break;
                case WidgetBarItem widget:
                    AddWidget(panel, widget);
                    break;
                case SpacerBarItem spacer:
                    panel.Children.Add(new Border
                    {
                        Width = Definition.Orientation == BarOrientation.Horizontal
                            ? (spacer.IsFlexible ? 24 : spacer.LogicalSize) : 1,
                        Height = Definition.Orientation == BarOrientation.Vertical
                            ? (spacer.IsFlexible ? 24 : spacer.LogicalSize) : 1,
                        MinWidth = spacer.IsFlexible ? 8 : 0,
                        MinHeight = spacer.IsFlexible ? 8 : 0,
                        HorizontalAlignment = spacer.IsFlexible
                            ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
                    });
                    break;
            }
        }
    }

    private async void AddApplicationItem(
        StackPanel panel,
        ApplicationIdentity identity,
        RunningApplicationGroup? group,
        bool isPinned)
    {
        var productSettings = _services.Settings().Normalize();
        var settings = productSettings.Appearance;
        var taskbar = productSettings.Taskbar;
        var displayName = _services.Applications.Current
            .FirstOrDefault(application => application.Identity == identity)?.DisplayName ??
            group?.DisplayName ?? Path.GetFileNameWithoutExtension(identity.Value);
        var iconSize = settings.ApplicationIconSize;
        var image = new Image { Width = iconSize, Height = iconSize, Stretch = Stretch.Uniform };
        var fallback = new FontIcon { Glyph = "\uE8FC", FontSize = iconSize * 0.72 };
        var iconHost = new Grid { Width = iconSize, Height = iconSize };
        iconHost.Children.Add(fallback);
        iconHost.Children.Add(image);
        var indicator = new Border
        {
            Height = group?.IsActive == true ? 3 : 2,
            Width = group is null ? 0 : group.Windows.Count > 1 ? 14 : 6,
            CornerRadius = new CornerRadius(2),
            Background = group?.IsActive == true
                ? ResolveAccentBrush(settings)
                : new SolidColorBrush(Color.FromArgb(180, 150, 160, 172)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = taskbar.ShowRunningIndicators ? Visibility.Visible : Visibility.Collapsed,
        };
        var item = new StackPanel { Spacing = 2 };
        item.Children.Add(iconHost);
        item.Children.Add(indicator);
        var button = new Button
        {
            Content = item,
            Padding = new Thickness(6, 4, 6, 3),
            MinWidth = iconSize + 12,
            MinHeight = iconSize + 12,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Colors.Transparent),
        };
        AutomationProperties.SetName(button, BuildAccessibleName(displayName, group, isPinned));
        if (taskbar.ShowTooltips) ToolTipService.SetToolTip(button, displayName);
        button.Click += (_, _) => Activate(identity, group, button);
        button.ContextFlyout = ApplicationMenu(identity, group, isPinned);
        button.PointerEntered += (_, _) =>
        {
            var index = _applicationElements.IndexOf(button);
            _services.Motion.ApplyMagnification(_applicationElements, index,
                settings.Magnification, settings.MagnificationMaximumScale);
        };
        button.PointerPressed += (_, _) =>
            _services.Motion.AnimateScale(button, MotionIntent.Press);
        button.PointerReleased += (_, _) =>
            _services.Motion.AnimateScale(button, MotionIntent.Hover, 1);
        _applicationElements.Add(button);
        panel.Children.Add(button);

        try
        {
            using var bitmap = _services.Icons.GetBitmap(identity, (int)iconSize,
                WindowPositioner.GetDpi(_nativeWindow.Hwnd));
            if (bitmap is not null)
            {
                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(bitmap);
                image.Source = source;
                fallback.Visibility = Visibility.Collapsed;
            }
        }
        catch { }
    }

    private void AddWidget(StackPanel panel, WidgetBarItem item)
    {
        if (!_services.WidgetRuntime.TryGet(
            new WidgetInstanceId(item.WidgetInstanceId), out var instance) || instance is null)
            return;
        var host = new Border
        {
            CornerRadius = new CornerRadius(10),
            MaxHeight = Math.Max(40, Definition.Thickness - 8),
            Child = _services.WidgetViews.Create(instance, _nativeWindow.Hwnd),
        };
        host.ContextFlyout = WidgetMenu(item.WidgetInstanceId);
        panel.Children.Add(host);
    }

    private MenuFlyout ApplicationMenu(
        ApplicationIdentity identity,
        RunningApplicationGroup? group,
        bool isPinned)
    {
        var flyout = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = group is null ? "Open" : "Activate" };
        open.Click += (_, _) => Activate(identity, group, null);
        flyout.Items.Add(open);
        var newInstance = new MenuFlyoutItem { Text = "New instance" };
        newInstance.Click += (_, _) => _launcher.Launch(identity);
        flyout.Items.Add(newInstance);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var pin = new MenuFlyoutItem { Text = isPinned ? "Unpin" : "Pin" };
        pin.Click += async (_, _) =>
        {
            if (isPinned) await _services.Shell().UnpinApplicationAsync(Id, identity);
            else await _services.Shell().PinApplicationAsync(Id, identity, BarZone.Center);
        };
        flyout.Items.Add(pin);
        if (isPinned)
        {
            var moveEarlier = new MenuFlyoutItem { Text = "Move earlier" };
            var moveLater = new MenuFlyoutItem { Text = "Move later" };
            moveEarlier.Click += async (_, _) => await MoveApplicationAsync(identity, -1);
            moveLater.Click += async (_, _) => await MoveApplicationAsync(identity, 1);
            flyout.Items.Add(moveEarlier);
            flyout.Items.Add(moveLater);
        }
        if (group is not null)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            var close = new MenuFlyoutItem { Text = "Close window" };
            close.Click += (_, _) => _launcher.RequestClose(group.Windows[0]);
            flyout.Items.Add(close);
            if (group.Windows.Count > 1)
            {
                var closeAll = new MenuFlyoutItem { Text = "Close all windows" };
                closeAll.Click += (_, _) => _launcher.RequestCloseAll(group.Windows);
                flyout.Items.Add(closeAll);
            }
        }
        return flyout;
    }

    private MenuFlyout WidgetMenu(Guid widgetId)
    {
        var flyout = new MenuFlyout();
        var customize = new MenuFlyoutItem { Text = "Customize" };
        customize.Click += (_, _) => _services.EditMode.Select(
            new EditSelection(EditableSurfaceKind.Widget, widgetId));
        var detach = new MenuFlyoutItem { Text = "Move to desktop" };
        detach.Click += async (_, _) =>
        {
            var instance = _services.Shell().Layout.WidgetInstances.First(widget =>
                widget.WidgetInstanceId == widgetId);
            await _services.Shell().MoveWidgetToDesktopAsync(widgetId,
                new Glass.Core.Placement.FloatingPlacement(
                    Definition.Placement.Target,
                    new Glass.Core.Geometry.LogicalRect(120, 120,
                        instance.Size.Width, instance.Size.Height)),
                SurfaceZOrder.Normal);
            _services.SyncWidgetSurfaces();
        };
        var moveToBar = new MenuFlyoutSubItem { Text = "Move to Bar" };
        foreach (var bar in _services.Shell().Layout.Bars)
        {
            var barMenu = new MenuFlyoutSubItem { Text = bar.Name };
            foreach (var zone in Enum.GetValues<BarZone>())
            {
                var destination = new MenuFlyoutItem { Text = zone.ToString() };
                destination.Click += async (_, _) => await _services.Shell().MoveWidgetToBarAsync(
                    widgetId, bar.Id, zone);
                barMenu.Items.Add(destination);
            }
            moveToBar.Items.Add(barMenu);
        }
        var remove = new MenuFlyoutItem { Text = "Remove" };
        remove.Click += async (_, _) => await _services.Shell().RemoveWidgetAsync(widgetId);
        flyout.Items.Add(customize);
        flyout.Items.Add(moveToBar);
        flyout.Items.Add(detach);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(remove);
        return flyout;
    }

    private async ValueTask MoveApplicationAsync(ApplicationIdentity identity, int direction)
    {
        var content = Definition.Content;
        var index = content.ToList().FindIndex(item =>
            item is PinnedApplicationBarItem pin && pin.Application == identity);
        if (index < 0) return;
        var target = Math.Clamp(index + direction, 0, content.Count - 1);
        await _services.Shell().MoveBarContentAsync(Id, index, target);
    }

    private void Activate(
        ApplicationIdentity identity,
        RunningApplicationGroup? group,
        Button? anchor)
    {
        var preferences = _services.Settings().Taskbar;
        if (group is null) _launcher.Launch(identity);
        else if (group.Windows.Count == 1 && preferences.ActivateSingleWindowDirectly)
            _launcher.ActivateOrToggle(group.Windows[0], preferences.ToggleForegroundWindowMinimize);
        else if (anchor is not null) ShowWindowChooser(anchor, group);
        else _launcher.ActivateOrToggle(group.Windows[0], preferences.ToggleForegroundWindowMinimize);
    }

    private void ShowWindowChooser(Button button, RunningApplicationGroup group)
    {
        var flyout = new MenuFlyout();
        foreach (var window in group.Windows)
        {
            var item = new MenuFlyoutItem
            {
                Text = string.IsNullOrWhiteSpace(window.Title) ? group.DisplayName : window.Title,
                Icon = window.IsForeground ? new FontIcon { Glyph = "\uE73E" } : null,
            };
            item.Click += (_, _) => _launcher.ActivateOrToggle(window,
                _services.Settings().Taskbar.ToggleForegroundWindowMinimize);
            flyout.Items.Add(item);
        }
        flyout.ShowAt(button);
    }

    private void Root_RightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (args.OriginalSource is FrameworkElement { Parent: Button }) return;
        var flyout = new MenuFlyout();
        var edit = new MenuFlyoutItem { Text = "Edit Bar" };
        edit.Click += (_, _) => _services.EditMode.Select(
            new EditSelection(EditableSurfaceKind.Bar, Id.Value));
        var addApp = new MenuFlyoutItem { Text = "Add App" };
        addApp.Click += (_, _) => _services.ShowControlCenter();
        var addWidget = new MenuFlyoutItem { Text = "Add Widget" };
        addWidget.Click += (_, _) => _services.ShowControlCenter();
        var autoHide = new ToggleMenuFlyoutItem
        {
            Text = "Auto-hide",
            IsChecked = Definition.AutoHideEnabled,
            IsEnabled = Definition.Placement is not Glass.Core.Placement.FloatingPlacement,
        };
        autoHide.Click += async (_, _) => await _services.Shell().UpdateBarAsync(
            Definition with { AutoHideEnabled = autoHide.IsChecked });
        var alwaysOnTop = new ToggleMenuFlyoutItem
        {
            Text = "Always on Top",
            IsChecked = Definition.ZOrder == SurfaceZOrder.AlwaysOnTop,
        };
        alwaysOnTop.Click += async (_, _) => await _services.Shell().UpdateBarAsync(
            Definition with
            {
                ZOrder = alwaysOnTop.IsChecked ? SurfaceZOrder.AlwaysOnTop : SurfaceZOrder.Normal,
            });
        var settings = new MenuFlyoutItem { Text = "Bar Settings" };
        settings.Click += (_, _) => _services.ShowControlCenter();
        var create = new MenuFlyoutItem { Text = "Create Another Bar" };
        create.Click += async (_, _) => await _services.Shell().CreateBarAsync(Definition.Placement.Target);
        var remove = new MenuFlyoutItem
        {
            Text = "Remove Bar",
            IsEnabled = _services.Shell().Layout.Bars.Count > 1,
        };
        remove.Click += async (_, _) => await _services.Shell().RemoveBarAsync(Id);
        var glassSettings = new MenuFlyoutItem { Text = "Open Glass Settings" };
        glassSettings.Click += (_, _) => _services.ShowControlCenter();
        flyout.Items.Add(edit);
        flyout.Items.Add(addApp);
        flyout.Items.Add(addWidget);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(autoHide);
        flyout.Items.Add(alwaysOnTop);
        flyout.Items.Add(settings);
        flyout.Items.Add(create);
        flyout.Items.Add(remove);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(glassSettings);
        flyout.ShowAt(Root, args.GetPosition(Root));
        args.Handled = true;
    }

    private void Root_PointerEntered(object sender, PointerRoutedEventArgs args) =>
        _autoHide.PointerEntered();

    private void Root_PointerExited(object sender, PointerRoutedEventArgs args)
    {
        _services.Motion.ResetMagnification(_applicationElements);
        _autoHide.PointerExited();
    }

    private void OnAutoHideStateChanged(AutoHideState state) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed) return;
            var hidden = state == AutoHideState.Hidden;
            _surfaceController.SetAutoHidden(hidden);
            SetHostedWidgetsVisible(IsVisible && !hidden);
            _services.Motion.AnimateOpacity(SurfaceChrome,
                hidden ? MotionIntent.AutoHide : MotionIntent.Reveal,
                hidden ? 0.18f : 1);
        });

    private void OnDefinitionSettled(object? sender, BarDefinitionChangedEventArgs args)
    {
        _services.Motion.AnimateScale(SurfaceChrome, MotionIntent.Snap, 1);
        DefinitionSettled?.Invoke(this, args);
    }

    private void OnRunningWindowsChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(RenderContent);

    private void OnEditModeChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            var selected = _services.EditMode.Selection is
                { Kind: EditableSurfaceKind.Bar } selection && selection.Id == Id.Value;
            EditOutline.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            EditLabelContainer.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            EditResizeHandle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            DragRegion.Opacity = _services.EditMode.IsActive ? 0.8 : 0.35;
            _services.Motion.AnimateScale(SurfaceChrome, MotionIntent.SurfaceLift,
                selected ? 1.015 : 1);
        });

    private void SetHostedWidgetsVisible(bool visible)
    {
        foreach (var widget in Definition.Content.OfType<WidgetBarItem>())
            _ = ObserveAsync(_services.WidgetRuntime.SetVisibleAsync(
                new WidgetInstanceId(widget.WidgetInstanceId), visible));
    }

    private static async Task ObserveAsync(ValueTask operation)
    {
        try { await operation; }
        catch (ObjectDisposedException) { }
    }

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();

    private StackPanel PanelFor(BarZone zone) => zone switch
    {
        BarZone.Start => StartPanel,
        BarZone.End => EndPanel,
        _ => CenterPanel,
    };
    private IEnumerable<StackPanel> Panels() => [StartPanel, CenterPanel, EndPanel];
    private IEnumerable<Border> Islands() => [StartIsland, CenterIsland, EndIsland];
    private static void SetZonePosition(FrameworkElement element, int column, int row)
    {
        Grid.SetColumn(element, column);
        Grid.SetRow(element, row);
    }
    private static string BuildAccessibleName(
        string displayName, RunningApplicationGroup? group, bool pinned)
    {
        var states = new List<string>();
        if (pinned) states.Add("pinned");
        if (group is not null) states.Add("running");
        if (group?.IsActive == true) states.Add("active");
        if (group?.Windows.Count > 1) states.Add($"{group.Windows.Count} windows");
        return states.Count == 0 ? displayName : $"{displayName}, {string.Join(", ", states)}";
    }

    private static Brush ResolveAccentBrush(GlobalAppearanceSettings settings)
    {
        if (settings.AccentPreference == AccentPreference.System)
        {
            try
            {
                if (Application.Current.Resources["AccentFillColorDefaultBrush"] is Brush accent)
                    return accent;
            }
            catch (KeyNotFoundException) { }
        }
        var value = settings.CustomAccentColor.TrimStart('#');
        var color = value.Length == 6
            ? Color.FromArgb(255,
                Convert.ToByte(value[0..2], 16),
                Convert.ToByte(value[2..4], 16),
                Convert.ToByte(value[4..6], 16))
            : Color.FromArgb(255, 86, 156, 255);
        return new SolidColorBrush(color);
    }
}
