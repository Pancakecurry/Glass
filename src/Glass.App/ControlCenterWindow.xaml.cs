using Glass.App.Configuration;
using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Product;
using Glass.Core.Runtime;
using Glass.Core.Shell;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Runtime;
using Glass.Platform.Windows.Windowing;
using Glass.Shell.Runtime;
using Glass.Widgets.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Globalization;
using Windows.ApplicationModel.DataTransfer;

namespace Glass.App;

public sealed partial class ControlCenterWindow : Window
{
    private readonly ShellRuntime _shell;
    private readonly WindowsDisplayService _displays;
    private readonly WindowsApplicationCatalog _applications;
    private readonly IReadOnlyList<WidgetMetadata> _widgets;
    private readonly AppearanceSettingsRuntime _settings;
    private readonly EditModeSession _editMode;
    private readonly Glass.Rendering.Materials.GlassMaterialController _materials;
    private readonly Func<RenderingPolicy> _renderingPolicy;
    private readonly Action _syncWidgets;
    private readonly Func<Task> _shutdown;
    private readonly PackagedStartupTaskService _startupTask;
    private readonly Func<string> _diagnosticsSnapshot;
    private readonly Func<bool, Task> _restart;
    private readonly Func<Task> _resetConfiguration;
    private readonly bool _safeMode;
    private bool _loading = true;
    private bool _closing;

    internal ControlCenterWindow(
        ShellRuntime shell,
        WindowsDisplayService displays,
        WindowsApplicationCatalog applications,
        IReadOnlyList<WidgetMetadata> widgets,
        AppearanceSettingsRuntime settings,
        EditModeSession editMode,
        Glass.Rendering.Materials.GlassMaterialController materials,
        Func<RenderingPolicy> renderingPolicy,
        Action syncWidgets,
        Func<Task> shutdown,
        PackagedStartupTaskService startupTask,
        Func<string> diagnosticsSnapshot,
        Func<bool, Task> restart,
        Func<Task> resetConfiguration,
        bool safeMode)
    {
        InitializeComponent();
        _shell = shell;
        _displays = displays;
        _applications = applications;
        _widgets = widgets;
        _settings = settings;
        _editMode = editMode;
        _materials = materials;
        _renderingPolicy = renderingPolicy;
        _syncWidgets = syncWidgets;
        _shutdown = shutdown;
        _startupTask = startupTask;
        _diagnosticsSnapshot = diagnosticsSnapshot;
        _restart = restart;
        _resetConfiguration = resetConfiguration;
        _safeMode = safeMode;
        Title = ProductBranding.ControlCenterWindowTitle;
        var native = WinUiWindowHandle.FromWindow(this);
        native.AppWindow.Resize(new Windows.Graphics.SizeInt32(1040, 760));
        materials.Apply(this, SurfaceChrome, settings.Current.Appearance.Material,
            settings.Current.Appearance.ThemeMode,
            Glass.Rendering.Materials.GlassSurfaceRole.ControlCenter,
            renderingPolicy());
        Closed += OnClosed;
        _shell.LayoutChanged += OnLayoutChanged;
        _settings.Changed += OnSettingsChanged;
        _editMode.Changed += OnEditModeChanged;
        _displays.DisplaysChanged += OnDisplaysChanged;
        Navigation.SelectedIndex = safeMode ? 5 : 0;
        if (safeMode) RestartButton.Content = "Try normal startup";
        RefreshAll();
        _loading = false;
    }

    public async void Present()
    {
        Activate();
        try
        {
            await RefreshApplicationsAsync();
            await LoadAdvancedAsync();
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private BarDefinition? SelectedBar =>
        (BarList.SelectedItem as ListViewItem)?.Tag as BarDefinition ??
        (_shell.Layout.Bars.Count > 0 ? _shell.Layout.Bars[0] : null);

    private WidgetMetadata? SelectedWidgetType =>
        (WidgetGallery.SelectedItem as ListViewItem)?.Tag as WidgetMetadata;

    private WidgetInstanceDefinition? SelectedWidgetInstance =>
        (WidgetInstances.SelectedItem as ListViewItem)?.Tag as WidgetInstanceDefinition;

    private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        var page = (Navigation.SelectedItem as ListViewItem)?.Tag as string ?? "Overview";
        foreach (var panel in Pages()) panel.Visibility = Visibility.Collapsed;
        Page(page).Visibility = Visibility.Visible;
        if (page == "Widgets") PopulateWidgets();
        if (page == "Bars") PopulateBars(SelectedBar?.Id);
        if (page == "Appearance") LoadAppearance();
        if (page == "Advanced") _ = LoadAdvancedObservedAsync();
    }

    private void RefreshAll()
    {
        var selectedBar = SelectedBar?.Id;
        PopulateDisplays();
        PopulateBars(selectedBar);
        PopulateWidgets();
        LoadAppearance();
        LoadBehavior();
        UpdateOverview();
    }

    private void UpdateOverview()
    {
        var settings = _settings.Current;
        OverviewBars.Text = _shell.Layout.Bars.Count.ToString(CultureInfo.CurrentCulture);
        OverviewWidgets.Text = _shell.Layout.WidgetInstances.Count.ToString(CultureInfo.CurrentCulture);
        OverviewTheme.Text = settings.Appearance.ThemeMode.ToString();
        OverviewMaterial.Text = settings.Appearance.Material.Preset.ToString();
        QuickTheme.SelectedIndex = (int)settings.Appearance.ThemeMode;
        StatusText.Text = _safeMode ? "Safe Mode · optional surfaces are preserved but not loaded" :
            _editMode.IsActive ? "Edit Mode" : "All changes save automatically";
        DiagnosticsText.Text = _diagnosticsSnapshot();
    }

    private void OnLayoutChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(RefreshAll);
    private void OnSettingsChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            _materials.Apply(this, SurfaceChrome, _settings.Current.Appearance.Material,
                _settings.Current.Appearance.ThemeMode,
                Glass.Rendering.Materials.GlassSurfaceRole.ControlCenter,
                _renderingPolicy());
            UpdateOverview();
            LoadAppearance();
            LoadBehavior();
        });
    private void OnEditModeChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(() => { UpdateOverview(); LoadAppearance(); });
    private void OnDisplaysChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(PopulateDisplays);

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        if (_closing) return;
        _closing = true;
        _shell.LayoutChanged -= OnLayoutChanged;
        _settings.Changed -= OnSettingsChanged;
        _editMode.Changed -= OnEditModeChanged;
        _displays.DisplaysChanged -= OnDisplaysChanged;
        await _shutdown();
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); StatusText.Text = "Saved"; }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async Task<string?> RequestNameAsync(string title, string initial)
    {
        var input = new TextBox { Text = initial, SelectionStart = 0, SelectionLength = initial.Length };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = input,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = SurfaceChrome.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text)
            ? input.Text.Trim() : null;
    }

    private MaterialSettings SelectedMaterial(GlassSettings settings) =>
        _editMode.Selection switch
        {
            { Kind: EditableSurfaceKind.Bar } value => AppearanceResolver.ForBar(settings, value.Id),
            { Kind: EditableSurfaceKind.Widget } value => AppearanceResolver.ForWidget(settings, value.Id),
            _ => settings.Appearance.Material,
        };

    private static WidgetInstanceDefinition NewWidget(WidgetMetadata metadata) => new(
        Guid.NewGuid(), metadata.TypeId.Value,
        new LogicalSize(metadata.SizeConstraints.Default.Width, metadata.SizeConstraints.Default.Height),
        new Dictionary<string, string>());

    private static TextBox SettingText(string header, string key, string value) => new()
    {
        Header = header,
        Text = value,
        Tag = key,
    };

    private static NumberBox SettingNumber(
        string header, string key, double value, double minimum, double maximum) => new()
    {
        Header = header,
        Value = value,
        Minimum = minimum,
        Maximum = maximum,
        Tag = key,
    };

    private static ToggleSwitch SettingToggle(
        string header, string key, bool value) => new()
    {
        Header = header,
        IsOn = value,
        Tag = key,
    };

    private static string Value(WidgetInstanceDefinition instance, string key) =>
        instance.Configuration.GetValueOrDefault(key) ?? string.Empty;

    private static double NumberValue(
        WidgetInstanceDefinition instance, string key, double fallback) =>
        instance.Configuration.TryGetValue(key, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;

    private string HostDescription(Guid id)
    {
        if (_shell.Layout.StandaloneWidgets.Any(widget => widget.WidgetInstanceId == id)) return "Desktop";
        var bar = _shell.Layout.Bars.FirstOrDefault(candidate => candidate.Content
            .OfType<WidgetBarItem>().Any(widget => widget.WidgetInstanceId == id));
        return bar is null ? "Unhosted" : bar.Name;
    }

    private static string Describe(BarContentItem item) => item switch
    {
        PinnedApplicationBarItem pin => $"App · {Path.GetFileNameWithoutExtension(pin.Application.Value)} · {pin.Zone}",
        WidgetBarItem => $"Widget · {item.Zone}",
        RunningApplicationsSlotBarItem => $"Running applications · {item.Zone}",
        SpacerBarItem spacer => $"{(spacer.IsFlexible ? "Flexible" : $"{spacer.LogicalSize:0} DIP")} spacer · {item.Zone}",
        _ => item.GetType().Name,
    };

    private IEnumerable<StackPanel> Pages() =>
        [OverviewPage, BarsPage, WidgetsPage, AppearancePage, BehaviorPage, AdvancedPage];
    private StackPanel Page(string name) => name switch
    {
        "Bars" => BarsPage,
        "Widgets" => WidgetsPage,
        "Appearance" => AppearancePage,
        "Behavior" => BehaviorPage,
        "Advanced" => AdvancedPage,
        _ => OverviewPage,
    };
    private static void Select(ComboBox comboBox, string value) =>
        comboBox.SelectedItem = comboBox.Items.OfType<object>().FirstOrDefault(item =>
            string.Equals(item is ComboBoxItem element ? element.Content?.ToString() : item.ToString(),
                value, StringComparison.OrdinalIgnoreCase));
    private static T Parse<T>(ComboBox comboBox, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>((comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ??
            comboBox.SelectedItem?.ToString(), out var value) ? value : fallback;
}
