using Glass.App.Configuration;
using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Shell.Runtime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Glass.Core.Applications;
using Glass.Widgets.Abstractions;

namespace Glass.App;

public sealed partial class DevelopmentShellControlsWindow : Window
{
    private readonly ShellRuntime _shell;
    private readonly WindowsDisplayService _displays;
    private readonly Func<Task> _shutdown;
    private readonly Action _syncWidgets;
    private bool _closing;

    public DevelopmentShellControlsWindow(
        ShellRuntime shell,
        WindowsDisplayService displays,
        IReadOnlyList<ApplicationDescriptor> applications,
        IReadOnlyList<WidgetMetadata> widgets,
        Action syncWidgets,
        Func<Task> shutdown)
    {
        InitializeComponent();
        _shell = shell;
        _displays = displays;
        _shutdown = shutdown;
        _syncWidgets = syncWidgets;
        Title = ProductBranding.DevelopmentControlsWindowTitle;
        Closed += OnClosed;
        _shell.RuntimeFaulted += OnRuntimeFaulted;
        _displays.DisplaysChanged += OnDisplaysChanged;
        PopulateDisplays();
        PopulateBars();
        foreach (var application in applications)
            ApplicationSelector.Items.Add(new ComboBoxItem
            {
                Content = application.DisplayName,
                Tag = application.Identity,
            });
        foreach (var widget in widgets)
            WidgetSelector.Items.Add(new ComboBoxItem
            {
                Content = widget.DisplayName,
                Tag = widget,
            });
    }

    public void Present()
    {
        Activate();
    }

    private BarDefinition? SelectedBar =>
        (BarList.SelectedItem as ListBoxItem)?.Tag as BarDefinition;

    private DisplayInfo SelectedDisplay =>
        (DisplaySelector.SelectedItem as ComboBoxItem)?.Tag as DisplayInfo ??
        _displays.PrimaryDisplay;

    private void PopulateDisplays()
    {
        var selectedId = SelectedDisplay.PersistentId;
        DisplaySelector.Items.Clear();
        foreach (var display in _displays.Displays)
        {
            DisplaySelector.Items.Add(new ComboBoxItem
            {
                Content = display.IsPrimary ? $"{display.Name} (Primary)" : display.Name,
                Tag = display,
            });
        }

        DisplaySelector.SelectedItem = DisplaySelector.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item =>
                item.Tag is DisplayInfo display && display.PersistentId == selectedId) ??
            DisplaySelector.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private void PopulateBars(BarId? selectedId = null)
    {
        selectedId ??= SelectedBar?.Id;
        BarList.Items.Clear();
        foreach (var bar in _shell.Layout.Bars)
        {
            BarList.Items.Add(new ListBoxItem
            {
                Content = $"{bar.Id} — {bar.Placement.GetType().Name} — {bar.Orientation}",
                Tag = bar,
            });
        }

        BarList.SelectedItem = BarList.Items
            .OfType<ListBoxItem>()
            .FirstOrDefault(item =>
                item.Tag is BarDefinition bar && bar.Id == selectedId) ??
            BarList.Items.OfType<ListBoxItem>().FirstOrDefault();
        UpdateDiagnostics("Ready");
    }

    private void LoadEditor(BarDefinition bar)
    {
        SelectTag(PlacementSelector, bar.Placement switch
        {
            FloatingPlacement => "Floating",
            AnchoredPlacement => "Anchored",
            DockedPlacement => "Docked",
            _ => "Anchored",
        });
        if (bar.Placement is AnchoredPlacement anchored)
        {
            SelectTag(EdgeSelector, anchored.Edge.ToString());
        }
        else if (bar.Placement is DockedPlacement docked)
        {
            SelectTag(EdgeSelector, docked.Edge.ToString());
        }

        SelectTag(OrientationSelector, bar.Orientation.ToString());
        SelectTag(LengthModeSelector, bar.LengthMode.ToString());
        LengthBox.Text = bar.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ThicknessBox.Text = bar.Thickness.ToString(System.Globalization.CultureInfo.InvariantCulture);
        AutoHideToggle.IsOn = bar.AutoHideEnabled;
        AlwaysOnTopToggle.IsOn = bar.ZOrder == SurfaceZOrder.AlwaysOnTop;
        EnabledToggle.IsOn = bar.IsEnabled;
    }

    private BarDefinition BuildDefinition(BarDefinition source)
    {
        var orientation = ReadEnum(OrientationSelector, BarOrientation.Horizontal);
        var edge = ReadEnum(EdgeSelector, ScreenEdge.Bottom);
        var length = ReadDouble(LengthBox, source.Length);
        var thickness = ReadDouble(ThicknessBox, source.Thickness);
        var target = WindowsDisplayService.ToTarget(SelectedDisplay);
        SurfacePlacement placement = ReadTag(PlacementSelector) switch
        {
            "Floating" => new FloatingPlacement(
                target,
                new LogicalRect(100, 100,
                    orientation == BarOrientation.Horizontal ? length : thickness,
                    orientation == BarOrientation.Horizontal ? thickness : length)),
            "Docked" => new DockedPlacement(target, edge, thickness),
            _ => new AnchoredPlacement(
                target,
                edge,
                0,
                new LogicalSize(
                    orientation == BarOrientation.Horizontal ? length : thickness,
                    orientation == BarOrientation.Horizontal ? thickness : length)),
        };

        return (source with
        {
            Placement = placement,
            Orientation = orientation,
            LengthMode = ReadEnum(LengthModeSelector, BarLengthMode.FitContent),
            Length = length,
            Thickness = thickness,
            AutoHideEnabled = AutoHideToggle.IsOn,
            ZOrder = AlwaysOnTopToggle.IsOn
                ? SurfaceZOrder.AlwaysOnTop
                : SurfaceZOrder.Normal,
            IsEnabled = EnabledToggle.IsOn,
        }).Normalize();
    }

    private async void Create_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            var bar = await _shell.CreateBarAsync(
                WindowsDisplayService.ToTarget(SelectedDisplay));
            PopulateBars(bar.Id);
        });

    private async void Remove_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            if (SelectedBar is { } bar)
            {
                await _shell.RemoveBarAsync(bar.Id);
                PopulateBars();
            }
        });

    private async void Apply_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            if (SelectedBar is { } bar)
            {
                var updated = BuildDefinition(bar);
                await _shell.UpdateBarAsync(updated);
                PopulateBars(updated.Id);
            }
        });

    private async void Reset_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            await _shell.ResetAsync();
            PopulateBars();
        });

    private void Show_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedBar is { } bar)
        {
            _shell.SetBarVisible(bar.Id, true);
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedBar is { } bar)
        {
            _shell.SetBarVisible(bar.Id, false);
        }
    }

    private async void PinApplication_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            if (SelectedBar is not { } bar ||
                (ApplicationSelector.SelectedItem as ComboBoxItem)?.Tag is not ApplicationIdentity app)
                return;
            await _shell.PinApplicationAsync(bar.Id, app, BarZone.Center);
            PopulateBars(bar.Id);
        });

    private async void UnpinApplication_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            if (SelectedBar is not { } bar ||
                (ApplicationSelector.SelectedItem as ComboBoxItem)?.Tag is not ApplicationIdentity app)
                return;
            await _shell.UnpinApplicationAsync(bar.Id, app);
            PopulateBars(bar.Id);
        });

    private async void AddBarWidget_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            if (SelectedBar is not { } bar ||
                (WidgetSelector.SelectedItem as ComboBoxItem)?.Tag is not WidgetMetadata metadata)
                return;
            var id = Guid.NewGuid();
            await _shell.AddWidgetToBarAsync(
                new WidgetInstanceDefinition(id, metadata.TypeId.Value,
                    new LogicalSize(metadata.SizeConstraints.Default.Width,
                        metadata.SizeConstraints.Default.Height),
                    new Dictionary<string, string>()),
                bar.Id,
                ReadEnum(WidgetZoneSelector, BarZone.Center));
            PopulateBars(bar.Id);
        });

    private async void AddStandaloneWidget_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () =>
        {
            if ((WidgetSelector.SelectedItem as ComboBoxItem)?.Tag is not WidgetMetadata metadata)
                return;
            var id = Guid.NewGuid();
            var size = new LogicalSize(metadata.SizeConstraints.Default.Width,
                metadata.SizeConstraints.Default.Height);
            await _shell.AddStandaloneWidgetAsync(
                new WidgetInstanceDefinition(id, metadata.TypeId.Value, size,
                    new Dictionary<string, string>()),
                new FloatingPlacement(WindowsDisplayService.ToTarget(SelectedDisplay),
                    new LogicalRect(120, 120, size.Width, size.Height)),
                SurfaceZOrder.Normal);
            _syncWidgets();
            PopulateBars();
        });

    private void BarList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (SelectedBar is { } bar)
        {
            LoadEditor(bar);
        }
    }

    private void OnDisplaysChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(PopulateDisplays);

    private void OnRuntimeFaulted(Exception exception) =>
        DispatcherQueue.TryEnqueue(() => UpdateDiagnostics(exception.Message));

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _shell.RuntimeFaulted -= OnRuntimeFaulted;
        _displays.DisplaysChanged -= OnDisplaysChanged;
        await _shutdown();
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            UpdateDiagnostics("Saved");
        }
        catch (Exception exception)
        {
            UpdateDiagnostics(exception.Message);
        }
    }

    private void UpdateDiagnostics(string status) =>
        DiagnosticsText.Text =
            $"{status} | Displays: {_displays.Displays.Count} | " +
            $"Bars: {_shell.Layout.Bars.Count} | Active surfaces: {_shell.Surfaces.Count}";

    private static string? ReadTag(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private static T ReadEnum<T>(ComboBox comboBox, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(ReadTag(comboBox), out var parsed) ? parsed : fallback;

    private static double ReadDouble(TextBox textBox, double fallback) =>
        double.TryParse(
            textBox.Text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : fallback;

    private static void SelectTag(ComboBox comboBox, string value) =>
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, value, StringComparison.Ordinal));
}
