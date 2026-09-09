using Glass.App.Configuration;
using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Shell;
using Glass.Platform.Windows.Applications;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;
using Glass.Shell.Runtime;
using Glass.Widgets.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Globalization;

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
    private readonly Action _syncWidgets;
    private readonly Func<Task> _shutdown;
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
        Action syncWidgets,
        Func<Task> shutdown)
    {
        InitializeComponent();
        _shell = shell;
        _displays = displays;
        _applications = applications;
        _widgets = widgets;
        _settings = settings;
        _editMode = editMode;
        _materials = materials;
        _syncWidgets = syncWidgets;
        _shutdown = shutdown;
        Title = ProductBranding.ControlCenterWindowTitle;
        var native = WinUiWindowHandle.FromWindow(this);
        native.AppWindow.Resize(new Windows.Graphics.SizeInt32(1040, 760));
        materials.Apply(this, SurfaceChrome, settings.Current.Appearance.Material,
            settings.Current.Appearance.ThemeMode,
            Glass.Rendering.Materials.GlassSurfaceRole.ControlCenter);
        Closed += OnClosed;
        _shell.LayoutChanged += OnLayoutChanged;
        _settings.Changed += OnSettingsChanged;
        _editMode.Changed += OnEditModeChanged;
        _displays.DisplaysChanged += OnDisplaysChanged;
        Navigation.SelectedIndex = 0;
        RefreshAll();
        _loading = false;
    }

    public void Present()
    {
        Activate();
        _ = RefreshApplicationsAsync();
    }

    private BarDefinition? SelectedBar =>
        (BarList.SelectedItem as ListViewItem)?.Tag as BarDefinition ?? _shell.Layout.Bars.FirstOrDefault();

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
        StatusText.Text = _editMode.IsActive ? "Edit Mode" : "All changes save automatically";
        DiagnosticsText.Text = $"Displays: {_displays.Displays.Count} · Bars: {_shell.Layout.Bars.Count} · " +
            $"Widgets: {_shell.Layout.WidgetInstances.Count} · Active surfaces: {_shell.Surfaces.Count}";
    }

    private void PopulateDisplays()
    {
        BarDisplay.Items.Clear();
        foreach (var display in _displays.Displays)
            BarDisplay.Items.Add(new ComboBoxItem
            {
                Content = display.IsPrimary ? $"{display.Name} (Primary)" : display.Name,
                Tag = display,
            });
    }

    private void PopulateBars(BarId? selectedId = null)
    {
        _loading = true;
        selectedId ??= SelectedBar?.Id;
        BarList.Items.Clear();
        foreach (var bar in _shell.Layout.Bars)
            BarList.Items.Add(new ListViewItem
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = bar.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = $"{bar.Placement.GetType().Name.Replace("Placement", string.Empty)} · {bar.Orientation}", Opacity = 0.65, FontSize = 12 },
                    },
                },
                Tag = bar,
            });
        BarList.SelectedItem = BarList.Items.OfType<ListViewItem>().FirstOrDefault(item =>
            item.Tag is BarDefinition bar && bar.Id == selectedId) ??
            BarList.Items.OfType<ListViewItem>().FirstOrDefault();
        if (SelectedBar is { } selected) LoadBar(selected);
        _loading = false;
    }

    private void LoadBar(BarDefinition bar)
    {
        _loading = true;
        BarName.Text = bar.Name;
        Select(BarPlacement, bar.Placement switch
        {
            FloatingPlacement => "Floating",
            DockedPlacement => "Docked",
            _ => "Anchored",
        });
        var edge = bar.Placement switch
        {
            AnchoredPlacement value => value.Edge,
            DockedPlacement value => value.Edge,
            _ => ScreenEdge.Bottom,
        };
        Select(BarEdge, edge.ToString());
        Select(BarOrientation, bar.Orientation.ToString());
        Select(BarLengthMode, bar.LengthMode.ToString());
        Select(BarVisualMode, bar.VisualMode.ToString());
        BarLength.Value = bar.Length;
        BarThickness.Value = bar.Thickness;
        BarAutoHide.IsOn = bar.AutoHideEnabled;
        BarTopmost.IsOn = bar.ZOrder == SurfaceZOrder.AlwaysOnTop;
        BarDisplay.SelectedItem = BarDisplay.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
            item.Tag is DisplayInfo display && display.PersistentId == bar.Placement.Target.PersistentId) ??
            BarDisplay.Items.OfType<ComboBoxItem>().FirstOrDefault();
        PopulateBarContent(bar);
        _loading = false;
    }

    private void PopulateBarContent(BarDefinition bar)
    {
        BarContentList.Items.Clear();
        foreach (var item in bar.Content)
            BarContentList.Items.Add(new ListViewItem { Content = Describe(item), Tag = item });
    }

    private void PopulateWidgets()
    {
        var selectedType = SelectedWidgetType?.TypeId;
        var query = WidgetSearch?.Text?.Trim() ?? string.Empty;
        WidgetGallery.Items.Clear();
        foreach (var widget in _widgets.Where(widget =>
            query.Length == 0 || widget.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            widget.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        {
            var network = widget.Capabilities.HasFlag(WidgetCapabilities.RequiresNetwork)
                ? " · Uses network when configured" : string.Empty;
            WidgetGallery.Items.Add(new ListViewItem
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = widget.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = widget.Description + network, Opacity = 0.68, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = $"Compact · Standard · Expanded", Opacity = 0.55, FontSize = 12 },
                    },
                },
                Tag = widget,
            });
        }
        WidgetGallery.SelectedItem = WidgetGallery.Items.OfType<ListViewItem>().FirstOrDefault(item =>
            item.Tag is WidgetMetadata metadata && metadata.TypeId == selectedType) ??
            WidgetGallery.Items.OfType<ListViewItem>().FirstOrDefault();

        var selectedId = SelectedWidgetInstance?.WidgetInstanceId;
        WidgetInstances.Items.Clear();
        foreach (var instance in _shell.Layout.WidgetInstances)
        {
            var metadata = _widgets.FirstOrDefault(widget => widget.TypeId.Value == instance.WidgetTypeId);
            var host = HostDescription(instance.WidgetInstanceId);
            WidgetInstances.Items.Add(new ListViewItem
            {
                Content = $"{metadata?.DisplayName ?? instance.WidgetTypeId} · {host} · {instance.Size.Width:0}×{instance.Size.Height:0}",
                Tag = instance,
            });
        }
        WidgetInstances.SelectedItem = WidgetInstances.Items.OfType<ListViewItem>().FirstOrDefault(item =>
            item.Tag is WidgetInstanceDefinition instance && instance.WidgetInstanceId == selectedId) ??
            WidgetInstances.Items.OfType<ListViewItem>().FirstOrDefault();
        PopulateWidgetSettings();
    }

    private void PopulateWidgetSettings()
    {
        if (WidgetSettingsPanel is null) return;
        WidgetSettingsPanel.Children.Clear();
        if (SelectedWidgetInstance is not { } instance)
        {
            WidgetSettingsPanel.Children.Add(new TextBlock
            {
                Text = "Select an instance to configure it.",
                Opacity = 0.65,
            });
            return;
        }

        WidgetSettingsPanel.Children.Add(SettingNumber(
            "Width", "__width", instance.Size.Width, 96, 800));
        WidgetSettingsPanel.Children.Add(SettingNumber(
            "Height", "__height", instance.Size.Height, 64, 800));
        switch (instance.WidgetTypeId)
        {
            case "clock":
            case "date":
                WidgetSettingsPanel.Children.Add(SettingText(
                    "Windows time-zone ID (blank uses local)", "timeZoneId",
                    Value(instance, "timeZoneId")));
                WidgetSettingsPanel.Children.Add(SettingToggle(
                    "Use 24-hour time", "use24Hour", Value(instance, "use24Hour") != "false"));
                WidgetSettingsPanel.Children.Add(SettingToggle(
                    "Show seconds", "showSeconds", Value(instance, "showSeconds") == "true"));
                break;
            case "timer":
                WidgetSettingsPanel.Children.Add(SettingNumber(
                    "Default minutes", "durationMinutes",
                    NumberValue(instance, "durationMinutes", 5), 1, 1440));
                break;
            case "weather":
                WidgetSettingsPanel.Children.Add(SettingText(
                    "Location label", "locationLabel", Value(instance, "locationLabel")));
                WidgetSettingsPanel.Children.Add(SettingNumber(
                    "Latitude", "latitude", NumberValue(instance, "latitude", double.NaN), -90, 90));
                WidgetSettingsPanel.Children.Add(SettingNumber(
                    "Longitude", "longitude", NumberValue(instance, "longitude", double.NaN), -180, 180));
                break;
        }
        var save = new Button { Content = "Save instance settings", MinHeight = 36 };
        save.Click += SaveWidgetSettings_Click;
        WidgetSettingsPanel.Children.Add(save);
    }

    private void LoadAppearance()
    {
        _loading = true;
        var settings = _settings.Current.Normalize();
        var material = SelectedMaterial(settings);
        Select(ThemeMode, settings.Appearance.ThemeMode.ToString());
        Select(AccentMode, settings.Appearance.AccentPreference.ToString());
        AccentColor.Text = settings.Appearance.CustomAccentColor;
        TintColor.Text = material.TintColor;
        MaterialIntensity.Value = material.MaterialIntensity;
        TintStrength.Value = material.TintStrength;
        Luminosity.Value = material.Luminosity;
        BorderStrength.Value = material.BorderStrength;
        EdgeStrength.Value = material.EdgeHighlightStrength;
        ShadowStrength.Value = material.ShadowStrength;
        CornerRadius.Value = material.CornerRadius;
        OverallOpacity.Value = material.OverallOpacity;
        BarPadding.Value = settings.Appearance.BarPadding;
        ItemSpacing.Value = settings.Appearance.ItemSpacing;
        IconSize.Value = settings.Appearance.ApplicationIconSize;
        Select(WidgetDensity, settings.Appearance.WidgetDensity.ToString());
        Select(Magnification, settings.Appearance.Magnification.ToString());
        MaximumScale.Value = settings.Appearance.MagnificationMaximumScale;
        PopulatePresets(material.Preset);
        ResetAppearance.Content = _editMode.Selection is null
            ? "Reset global defaults" : "Reset to Global";
        _loading = false;
    }

    private void PopulatePresets(MaterialPreset current)
    {
        PresetSelector.Items.Clear();
        foreach (var preset in AppearancePresets.BuiltIn.Concat(_settings.Current.CustomPresets))
            PresetSelector.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset });
        PresetSelector.SelectedItem = PresetSelector.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag is AppearancePresetDefinition preset &&
                preset.Material.Preset == current);
    }

    private void LoadBehavior()
    {
        _loading = true;
        var settings = _settings.Current;
        Select(MotionMode, settings.Appearance.MotionPreference.ToString());
        DirectActivation.IsOn = settings.Taskbar.ActivateSingleWindowDirectly;
        ToggleMinimize.IsOn = settings.Taskbar.ToggleForegroundWindowMinimize;
        RunningIndicators.IsOn = settings.Taskbar.ShowRunningIndicators;
        ShowTooltips.IsOn = settings.Taskbar.ShowTooltips;
        _loading = false;
    }

    private async void QuickTheme_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_loading || QuickTheme.SelectedItem is not string value ||
            !Enum.TryParse<Glass.Core.Appearance.ThemeMode>(value, out var mode)) return;
        await _settings.UpdateAsync(current => current with
        {
            Appearance = current.Appearance with { ThemeMode = mode },
        });
    }

    private void EditDesktop_Click(object sender, RoutedEventArgs args)
    {
        _editMode.Enter();
        StatusText.Text = "Edit Mode · select a bar or widget on the desktop";
    }

    private void FinishEditing_Click(object sender, RoutedEventArgs args) => _editMode.Exit();

    private async void QuickAddBar_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () => await _shell.CreateBarAsync(
            WindowsDisplayService.ToTarget(_displays.PrimaryDisplay)));

    private void QuickAddWidget_Click(object sender, RoutedEventArgs args)
    {
        Navigation.SelectedIndex = 2;
        WidgetSearch.Focus(FocusState.Programmatic);
    }

    private async void AddBar_Click(object sender, RoutedEventArgs args) =>
        await RunAsync(async () => await _shell.CreateBarAsync(
            WindowsDisplayService.ToTarget(_displays.PrimaryDisplay)));

    private async void DuplicateBar_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedBar is not { } source) return;
        await RunAsync(async () =>
        {
            var created = await _shell.CreateBarAsync(source.Placement.Target);
            var safeContent = source.Content.Where(item => item is not WidgetBarItem).ToArray();
            await _shell.UpdateBarAsync(source with
            {
                Id = created.Id,
                Name = $"{source.Name} Copy",
                Content = safeContent,
            });
        });
    }

    private async void RemoveBar_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedBar is not { } bar || _shell.Layout.Bars.Count <= 1) return;
        await RunAsync(async () => await _shell.RemoveBarAsync(bar.Id));
    }

    private void BarList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_loading && SelectedBar is { } bar) LoadBar(bar);
    }

    private async void BarEditor_Changed(object sender, object args) => await ApplyBarEditorAsync();
    private async void BarNumber_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => await ApplyBarEditorAsync();
    private async void BarToggle_Changed(object sender, RoutedEventArgs args) => await ApplyBarEditorAsync();

    private async Task ApplyBarEditorAsync()
    {
        if (_loading || SelectedBar is not { } source || double.IsNaN(BarLength.Value) ||
            double.IsNaN(BarThickness.Value)) return;
        var display = (BarDisplay.SelectedItem as ComboBoxItem)?.Tag as DisplayInfo ?? _displays.PrimaryDisplay;
        var target = WindowsDisplayService.ToTarget(display);
        var orientation = Parse(BarOrientation, Glass.Core.Shell.BarOrientation.Horizontal);
        var edge = Parse(BarEdge, ScreenEdge.Bottom);
        var length = BarLength.Value;
        var thickness = BarThickness.Value;
        SurfacePlacement placement = (BarPlacement.SelectedItem as string) switch
        {
            "Floating" => new FloatingPlacement(target,
                new LogicalRect(100, 100,
                    orientation == Glass.Core.Shell.BarOrientation.Horizontal ? length : thickness,
                    orientation == Glass.Core.Shell.BarOrientation.Horizontal ? thickness : length)),
            "Docked" => new DockedPlacement(target, edge, thickness),
            _ => new AnchoredPlacement(target, edge, 0.5,
                new LogicalSize(
                    orientation == Glass.Core.Shell.BarOrientation.Horizontal ? length : thickness,
                    orientation == Glass.Core.Shell.BarOrientation.Horizontal ? thickness : length)),
        };
        var updated = (source with
        {
            Name = BarName.Text,
            Placement = placement,
            Orientation = orientation,
            LengthMode = Parse(BarLengthMode, Glass.Core.Shell.BarLengthMode.FitContent),
            VisualMode = Parse(BarVisualMode, Glass.Core.Appearance.BarVisualMode.Unified),
            Length = length,
            Thickness = thickness,
            AutoHideEnabled = BarAutoHide.IsOn,
            ZOrder = BarTopmost.IsOn ? SurfaceZOrder.AlwaysOnTop : SurfaceZOrder.Normal,
        }).Normalize();
        await RunAsync(async () => await _shell.UpdateBarAsync(updated));
    }

    private async void ContentEarlier_Click(object sender, RoutedEventArgs args) =>
        await MoveSelectedContentAsync(-1);
    private async void ContentLater_Click(object sender, RoutedEventArgs args) =>
        await MoveSelectedContentAsync(1);
    private async Task MoveSelectedContentAsync(int direction)
    {
        if (SelectedBar is not { } bar || BarContentList.SelectedIndex < 0) return;
        var from = BarContentList.SelectedIndex;
        var to = Math.Clamp(from + direction, 0, bar.Content.Count - 1);
        await RunAsync(async () => await _shell.MoveBarContentAsync(bar.Id, from, to));
    }
    private async void RemoveContent_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedBar is not { } bar || BarContentList.SelectedIndex < 0) return;
        var item = bar.Content[BarContentList.SelectedIndex];
        if (item is RunningApplicationsSlotBarItem) return;
        if (item is WidgetBarItem widget)
            await RunAsync(async () => await _shell.RemoveWidgetAsync(widget.WidgetInstanceId));
        else
            await RunAsync(async () => await _shell.UpdateBarContentAsync(bar.Id,
                bar.Content.Where(candidate => !ReferenceEquals(candidate, item)).ToArray()));
    }
    private async void AddFixedSpacer_Click(object sender, RoutedEventArgs args) => await AddSpacerAsync(false);
    private async void AddFlexibleSpacer_Click(object sender, RoutedEventArgs args) => await AddSpacerAsync(true);
    private async Task AddSpacerAsync(bool flexible)
    {
        if (SelectedBar is not { } bar) return;
        await RunAsync(async () => await _shell.UpdateBarContentAsync(bar.Id,
            [.. bar.Content, new SpacerBarItem(BarZone.Center, flexible, flexible ? 8 : 16)]));
    }

    private async void ApplicationSelector_DropDownOpened(object sender, object args) =>
        await RefreshApplicationsAsync();
    private async void AddApplication_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedBar is not { } bar ||
            (ApplicationSelector.SelectedItem as ComboBoxItem)?.Tag is not ApplicationIdentity identity) return;
        await RunAsync(async () => await _shell.PinApplicationAsync(bar.Id, identity, BarZone.Center));
    }
    private async void RefreshApplications_Click(object sender, RoutedEventArgs args) =>
        await RefreshApplicationsAsync();
    private async Task RefreshApplicationsAsync()
    {
        await Task.Yield();
        try
        {
            var current = _applications.Refresh();
            ApplicationSelector.Items.Clear();
            foreach (var application in current)
                ApplicationSelector.Items.Add(new ComboBoxItem
                {
                    Content = application.DisplayName,
                    Tag = application.Identity,
                });
            StatusText.Text = $"{current.Count} applications available";
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void WidgetSearch_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_loading) PopulateWidgets();
    }
    private void WidgetInstances_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_loading) PopulateWidgetSettings();
    }
    private async void AddWidgetDesktop_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedWidgetType is not { } metadata) return;
        var instance = NewWidget(metadata);
        await RunAsync(async () => await _shell.AddStandaloneWidgetAsync(instance,
            new FloatingPlacement(WindowsDisplayService.ToTarget(_displays.PrimaryDisplay),
                new LogicalRect(120, 120, instance.Size.Width, instance.Size.Height)),
            SurfaceZOrder.Normal));
        _syncWidgets();
    }
    private async void AddWidgetBar_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedWidgetType is not { } metadata || SelectedBar is not { } bar) return;
        await RunAsync(async () => await _shell.AddWidgetToBarAsync(NewWidget(metadata), bar.Id, BarZone.Center));
    }
    private void ConfigureWidget_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedWidgetInstance is { } widget)
        {
            _editMode.Select(new EditSelection(EditableSurfaceKind.Widget, widget.WidgetInstanceId));
            PopulateWidgetSettings();
        }
    }
    private async void SaveWidgetSettings_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedWidgetInstance is not { } instance) return;
        var values = instance.Configuration.ToDictionary(pair => pair.Key, pair => pair.Value);
        var width = instance.Size.Width;
        var height = instance.Size.Height;
        foreach (var control in WidgetSettingsPanel.Children.OfType<FrameworkElement>())
        {
            if (control.Tag is not string key) continue;
            string? value = control switch
            {
                TextBox text => text.Text.Trim(),
                ToggleSwitch toggle => toggle.IsOn ? "true" : "false",
                NumberBox number when !double.IsNaN(number.Value) =>
                    number.Value.ToString(CultureInfo.InvariantCulture),
                _ => null,
            };
            if (key == "__width" && value is not null)
                width = double.Parse(value, CultureInfo.InvariantCulture);
            else if (key == "__height" && value is not null)
                height = double.Parse(value, CultureInfo.InvariantCulture);
            else if (value is null || value.Length == 0) values.Remove(key);
            else values[key] = value;
        }
        await RunAsync(async () => await _shell.UpdateWidgetConfigurationAsync(instance with
        {
            Size = new LogicalSize(width, height),
            Configuration = values,
        }));
        _syncWidgets();
    }
    private async void MoveWidget_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedWidgetInstance is not { } widget) return;
        var isDesktop = _shell.Layout.StandaloneWidgets.Any(surface =>
            surface.WidgetInstanceId == widget.WidgetInstanceId);
        if (isDesktop && SelectedBar is { } bar)
            await RunAsync(async () => await _shell.MoveWidgetToBarAsync(
                widget.WidgetInstanceId, bar.Id, BarZone.Center));
        else
            await RunAsync(async () => await _shell.MoveWidgetToDesktopAsync(
                widget.WidgetInstanceId,
                new FloatingPlacement(WindowsDisplayService.ToTarget(_displays.PrimaryDisplay),
                    new LogicalRect(140, 140, widget.Size.Width, widget.Size.Height)),
                SurfaceZOrder.Normal));
        _syncWidgets();
    }
    private async void DuplicateWidget_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedWidgetInstance is not { } widget) return;
        await RunAsync(async () => await _shell.DuplicateWidgetAsync(widget.WidgetInstanceId));
        _syncWidgets();
    }
    private async void RemoveWidget_Click(object sender, RoutedEventArgs args)
    {
        if (SelectedWidgetInstance is not { } widget) return;
        await RunAsync(async () => await _shell.RemoveWidgetAsync(widget.WidgetInstanceId));
        _syncWidgets();
    }

    private async void PresetSelector_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_loading || (PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not AppearancePresetDefinition preset) return;
        await ApplyMaterialAsync(preset.Material);
    }
    private async void Appearance_Changed(object sender, object args)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, ThemeMode) && Enum.TryParse<Glass.Core.Appearance.ThemeMode>(ThemeMode.SelectedItem as string, out var theme))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { ThemeMode = theme },
            });
        else if (ReferenceEquals(sender, AccentMode) && Enum.TryParse<AccentPreference>(AccentMode.SelectedItem as string, out var accent))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { AccentPreference = accent },
            });
        else if (ReferenceEquals(sender, AccentColor))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { CustomAccentColor = AccentColor.Text },
            });
        else if (ReferenceEquals(sender, Magnification) && Enum.TryParse<MagnificationMode>(Magnification.SelectedItem as string, out var magnification))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { Magnification = magnification },
            });
        else if (ReferenceEquals(sender, WidgetDensity) && Enum.TryParse<WidgetSurfaceDensity>(WidgetDensity.SelectedItem as string, out var density))
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { WidgetDensity = density },
            });
        else if (ReferenceEquals(sender, TintColor))
            await ApplyMaterialOverrideAsync(value => value with { TintColor = TintColor.Text },
                value => value with { TintColor = TintColor.Text });
    }
    private async void AppearanceSlider_Changed(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, BarPadding) || ReferenceEquals(sender, ItemSpacing) ||
            ReferenceEquals(sender, IconSize) || ReferenceEquals(sender, MaximumScale))
        {
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with
                {
                    BarPadding = BarPadding.Value,
                    ItemSpacing = ItemSpacing.Value,
                    ApplicationIconSize = IconSize.Value,
                    MagnificationMaximumScale = MaximumScale.Value,
                },
            });
            return;
        }
        await ApplyMaterialOverrideAsync(material => sender switch
        {
            Slider value when ReferenceEquals(value, MaterialIntensity) => material with { MaterialIntensity = value.Value },
            Slider value when ReferenceEquals(value, TintStrength) => material with { TintStrength = value.Value },
            Slider value when ReferenceEquals(value, Luminosity) => material with { Luminosity = value.Value },
            Slider value when ReferenceEquals(value, BorderStrength) => material with { BorderStrength = value.Value },
            Slider value when ReferenceEquals(value, EdgeStrength) => material with { EdgeHighlightStrength = value.Value },
            Slider value when ReferenceEquals(value, ShadowStrength) => material with { ShadowStrength = value.Value },
            Slider value when ReferenceEquals(value, CornerRadius) => material with { CornerRadius = value.Value },
            Slider value when ReferenceEquals(value, OverallOpacity) => material with { OverallOpacity = value.Value },
            _ => material,
        }, value => sender switch
        {
            Slider slider when ReferenceEquals(slider, MaterialIntensity) => value with { MaterialIntensity = slider.Value },
            Slider slider when ReferenceEquals(slider, TintStrength) => value with { TintStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, Luminosity) => value with { Luminosity = slider.Value },
            Slider slider when ReferenceEquals(slider, BorderStrength) => value with { BorderStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, EdgeStrength) => value with { EdgeHighlightStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, ShadowStrength) => value with { ShadowStrength = slider.Value },
            Slider slider when ReferenceEquals(slider, CornerRadius) => value with { CornerRadius = slider.Value },
            Slider slider when ReferenceEquals(slider, OverallOpacity) => value with { OverallOpacity = slider.Value },
            _ => value,
        });
    }

    private async Task ApplyMaterialAsync(MaterialSettings material)
    {
        if (_editMode.Selection is not { } selection)
        {
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with { Material = material },
            });
            return;
        }
        var full = new MaterialOverride
        {
            Preset = material.Preset,
            TintColor = material.TintColor,
            TintStrength = material.TintStrength,
            MaterialIntensity = material.MaterialIntensity,
            Luminosity = material.Luminosity,
            BorderStrength = material.BorderStrength,
            EdgeHighlightStrength = material.EdgeHighlightStrength,
            ShadowStrength = material.ShadowStrength,
            CornerRadius = material.CornerRadius,
            OverallOpacity = material.OverallOpacity,
        };
        await UpdateOverrideAsync(selection, _ => full);
    }

    private async Task ApplyMaterialOverrideAsync(
        Func<MaterialSettings, MaterialSettings> global,
        Func<MaterialOverride, MaterialOverride> surface)
    {
        if (_editMode.Selection is { } selection)
            await UpdateOverrideAsync(selection, surface);
        else
            await _settings.UpdateAsync(current => current with
            {
                Appearance = current.Appearance with
                {
                    Material = global(current.Appearance.Material),
                },
            });
    }

    private async Task UpdateOverrideAsync(
        EditSelection selection,
        Func<MaterialOverride, MaterialOverride> update)
    {
        await _settings.UpdateAsync(current =>
        {
            var source = selection.Kind == EditableSurfaceKind.Bar
                ? current.BarAppearanceOverrides : current.WidgetAppearanceOverrides;
            var values = source.ToDictionary(pair => pair.Key, pair => pair.Value);
            values.TryGetValue(selection.Id, out var existing);
            values[selection.Id] = update(existing ?? new MaterialOverride());
            return selection.Kind == EditableSurfaceKind.Bar
                ? current with { BarAppearanceOverrides = values }
                : current with { WidgetAppearanceOverrides = values };
        });
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs args)
    {
        var name = await RequestNameAsync("Save appearance preset", "My Glass preset");
        if (name is null) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = [.. current.CustomPresets,
                new AppearancePresetDefinition(Guid.NewGuid(), name,
                    SelectedMaterial(current) with { Preset = MaterialPreset.Custom }, false)],
        });
    }
    private async void DuplicatePreset_Click(object sender, RoutedEventArgs args)
    {
        if ((PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not AppearancePresetDefinition preset) return;
        var name = await RequestNameAsync("Duplicate preset", $"{preset.Name} Copy");
        if (name is null) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = [.. current.CustomPresets,
                new AppearancePresetDefinition(Guid.NewGuid(), name,
                    preset.Material with { Preset = MaterialPreset.Custom }, false)],
        });
    }
    private async void RenamePreset_Click(object sender, RoutedEventArgs args)
    {
        if ((PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not
            AppearancePresetDefinition { IsBuiltIn: false } preset) return;
        var name = await RequestNameAsync("Rename preset", preset.Name);
        if (name is null) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = current.CustomPresets
                .Select(item => item.PresetId == preset.PresetId ? item with { Name = name } : item)
                .ToArray(),
        });
    }
    private async void DeletePreset_Click(object sender, RoutedEventArgs args)
    {
        if ((PresetSelector.SelectedItem as ComboBoxItem)?.Tag is not AppearancePresetDefinition { IsBuiltIn: false } preset) return;
        await _settings.UpdateAsync(current => current with
        {
            CustomPresets = current.CustomPresets.Where(item => item.PresetId != preset.PresetId).ToArray(),
        });
    }
    private async void ResetAppearance_Click(object sender, RoutedEventArgs args)
    {
        if (_editMode.Selection is { } selection)
        {
            await _settings.UpdateAsync(current =>
            {
                var bars = current.BarAppearanceOverrides.Where(pair => pair.Key != selection.Id)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                var widgets = current.WidgetAppearanceOverrides.Where(pair => pair.Key != selection.Id)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                return current with
                {
                    BarAppearanceOverrides = selection.Kind == EditableSurfaceKind.Bar ? bars : current.BarAppearanceOverrides,
                    WidgetAppearanceOverrides = selection.Kind == EditableSurfaceKind.Widget ? widgets : current.WidgetAppearanceOverrides,
                };
            });
        }
        else await _settings.UpdateAsync(current => current with { Appearance = new GlobalAppearanceSettings() });
    }

    private async void Behavior_Changed(object sender, object args)
    {
        if (_loading) return;
        await _settings.UpdateAsync(current => current with
        {
            Appearance = current.Appearance with
            {
                MotionPreference = Parse(MotionMode, MotionPreference.System),
            },
            Taskbar = current.Taskbar with
            {
                ActivateSingleWindowDirectly = DirectActivation.IsOn,
                ToggleForegroundWindowMinimize = ToggleMinimize.IsOn,
                ShowRunningIndicators = RunningIndicators.IsOn,
                ShowTooltips = ShowTooltips.IsOn,
            },
        });
    }

    private async void Exit_Click(object sender, RoutedEventArgs args) => await _shutdown();

    private void OnLayoutChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(RefreshAll);
    private void OnSettingsChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            _materials.Apply(this, SurfaceChrome, _settings.Current.Appearance.Material,
                _settings.Current.Appearance.ThemeMode,
                Glass.Rendering.Materials.GlassSurfaceRole.ControlCenter);
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
