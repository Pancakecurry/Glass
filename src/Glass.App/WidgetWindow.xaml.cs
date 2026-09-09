using Glass.App.Runtime;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Glass.Widgets.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Glass.App;

public sealed partial class WidgetWindow : Window, IDisposable
{
    private readonly WindowsDisplayService _displays;
    private readonly ProductSurfaceServices _services;
    private readonly WinUiWindowHandle _native;
    private readonly NativeWindowMessageRouter _messages;
    private readonly IDisposable _moveRegistration;
    private readonly IDisposable _dpiRegistration;
    private bool _disposed;
    private bool _presented;

    internal WidgetWindow(
        WindowsDisplayService displays,
        ProductSurfaceServices services,
        StandaloneWidgetDefinition surface,
        WidgetInstanceDefinition instance)
    {
        InitializeComponent();
        _displays = displays;
        _services = services;
        Surface = surface;
        Instance = instance;
        ExtendsContentIntoTitleBar = true;
        _native = WinUiWindowHandle.FromWindow(this);
        if (_native.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
        ApplyLockState();
        ShellWindowStyle.Apply(_native.Hwnd, surface.ZOrder);
        _messages = new NativeWindowMessageRouter(_native.Hwnd);
        _moveRegistration = _messages.Register(NativeWindowMessages.ExitSizeMove, OnSettled);
        _dpiRegistration = _messages.Register(NativeWindowMessages.DpiChanged, OnDpiChanged);
        _services.EditMode.Changed += OnEditModeChanged;
        Apply(surface, instance);
        Closed += OnClosed;
    }

    public StandaloneWidgetDefinition Surface { get; private set; }
    public WidgetInstanceDefinition Instance { get; private set; }
    public event Action<StandaloneWidgetDefinition>? DefinitionSettled;

    public void Apply(StandaloneWidgetDefinition surface, WidgetInstanceDefinition instance)
    {
        Surface = surface;
        Instance = instance;
        Title = BuiltInTitle(instance.WidgetTypeId);
        EditLabel.Text = $"{Title} widget";
        ApplyLockState();
        ApplyAppearance();
        ApplyPlacement(surface.Placement, instance.Size);
        if (_services.WidgetRuntime.TryGet(
            new WidgetInstanceId(instance.WidgetInstanceId), out var runtime) && runtime is not null)
        {
            WidgetContent.Content = _services.WidgetViews.Create(runtime, _native.Hwnd);
            if (_presented)
                _ = ObserveAsync(_services.WidgetRuntime.SetVisibleAsync(
                    new WidgetInstanceId(instance.WidgetInstanceId), true));
        }
        OnEditModeChanged(this, EventArgs.Empty);
    }

    public void Present()
    {
        _presented = true;
        _native.AppWindow.Show(false);
        _ = ObserveAsync(_services.WidgetRuntime.SetVisibleAsync(
            new WidgetInstanceId(Instance.WidgetInstanceId), true));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Closed -= OnClosed;
        _services.EditMode.Changed -= OnEditModeChanged;
        _ = ObserveAsync(_services.WidgetRuntime.SetVisibleAsync(
            new WidgetInstanceId(Instance.WidgetInstanceId), false));
        _dpiRegistration.Dispose();
        _moveRegistration.Dispose();
        _messages.Dispose();
        DefinitionSettled = null;
    }

    private void ApplyAppearance()
    {
        var settings = _services.Settings().Normalize();
        var material = AppearanceResolver.ForWidget(settings, Instance.WidgetInstanceId);
        _services.Materials.Apply(this, SurfaceChrome, material,
            settings.Appearance.ThemeMode, Glass.Rendering.Materials.GlassSurfaceRole.Widget);
        EditOutline.CornerRadius = new CornerRadius(material.CornerRadius + 3);
    }

    private void ApplyLockState()
    {
        var editing = _services.EditMode.IsActive;
        var locked = Surface.IsLocked && !editing;
        SetTitleBar(locked ? null : DragRegion);
        if (_native.AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsResizable = !locked;
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

    private void Root_RightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        var flyout = new MenuFlyout();
        var customize = new MenuFlyoutItem { Text = "Customize" };
        customize.Click += (_, _) => _services.EditMode.Select(
            new EditSelection(EditableSurfaceKind.Widget, Instance.WidgetInstanceId));
        var sizes = new MenuFlyoutSubItem { Text = "Resize preset" };
        foreach (var (name, size) in new[]
        {
            ("Compact", new LogicalSize(180, 96)),
            ("Standard", new LogicalSize(280, 180)),
            ("Expanded", new LogicalSize(380, 300)),
        })
        {
            var item = new MenuFlyoutItem { Text = name };
            item.Click += (_, _) => Resize(size);
            sizes.Items.Add(item);
        }
        var topmost = new ToggleMenuFlyoutItem
        {
            Text = "Always on Top",
            IsChecked = Surface.ZOrder == SurfaceZOrder.AlwaysOnTop,
        };
        topmost.Click += async (_, _) =>
        {
            Surface = Surface with
            {
                ZOrder = topmost.IsChecked ? SurfaceZOrder.AlwaysOnTop : SurfaceZOrder.Normal,
            };
            ShellWindowStyle.Apply(_native.Hwnd, Surface.ZOrder);
            await _services.Shell().UpdateStandaloneWidgetAsync(Surface);
        };
        var attach = new MenuFlyoutSubItem { Text = "Attach to Bar" };
        foreach (var bar in _services.Shell().Layout.Bars)
        {
            var item = new MenuFlyoutItem { Text = bar.Name };
            item.Click += async (_, _) =>
            {
                await _services.Shell().MoveWidgetToBarAsync(
                    Instance.WidgetInstanceId, bar.Id, BarZone.Center);
                _services.SyncWidgetSurfaces();
            };
            attach.Items.Add(item);
        }
        var duplicate = new MenuFlyoutItem { Text = "Duplicate" };
        duplicate.Click += async (_, _) =>
        {
            await _services.Shell().DuplicateWidgetAsync(Instance.WidgetInstanceId);
            _services.SyncWidgetSurfaces();
        };
        var locked = new ToggleMenuFlyoutItem { Text = "Lock", IsChecked = Surface.IsLocked };
        locked.Click += async (_, _) =>
        {
            Surface = Surface with { IsLocked = locked.IsChecked };
            ApplyLockState();
            await _services.Shell().UpdateStandaloneWidgetAsync(Surface);
        };
        var remove = new MenuFlyoutItem { Text = "Remove" };
        remove.Click += async (_, _) =>
        {
            await _services.Shell().RemoveWidgetAsync(Instance.WidgetInstanceId);
            _services.SyncWidgetSurfaces();
        };
        flyout.Items.Add(customize);
        flyout.Items.Add(sizes);
        flyout.Items.Add(topmost);
        flyout.Items.Add(attach);
        flyout.Items.Add(duplicate);
        flyout.Items.Add(locked);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(remove);
        flyout.ShowAt(Root, args.GetPosition(Root));
        args.Handled = true;
    }

    private void Resize(LogicalSize size)
    {
        var bounds = WindowPositioner.GetBounds(_native.Hwnd);
        var display = _displays.GetForWindow(_native.WindowId);
        var dpi = WindowPositioner.GetDpi(_native.Hwnd);
        var logical = DpiConverter.ToLogical(bounds, display.WorkArea, dpi) with
        {
            Width = size.Width,
            Height = size.Height,
        };
        WindowPositioner.MoveAndResize(_native.Hwnd,
            DpiConverter.ToNative(logical, display.WorkArea, dpi));
        Capture(dpi, WindowPositioner.GetBounds(_native.Hwnd));
    }

    private void OnEditModeChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            var selected = _services.EditMode.Selection is
                { Kind: EditableSurfaceKind.Widget } selection &&
                selection.Id == Instance.WidgetInstanceId;
            EditOutline.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            EditLabelContainer.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            EditResizeHandle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            ApplyLockState();
            _services.Motion.AnimateScale(SurfaceChrome,
                Glass.Core.Appearance.MotionIntent.SurfaceLift, selected ? 1.015 : 1);
        });

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();

    private static string BuiltInTitle(string typeId) =>
        Glass.Widgets.BuiltIn.BuiltInWidgetCatalog.All
            .FirstOrDefault(metadata => metadata.TypeId.Value == typeId)?.DisplayName ?? "Glass Widget";

    private static async Task ObserveAsync(ValueTask operation)
    {
        try { await operation; }
        catch (ObjectDisposedException) { }
    }
}
