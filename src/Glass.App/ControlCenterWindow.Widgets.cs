using Glass.App.Configuration;
using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Core.Appearance;
using Glass.Core.Editing;
using Glass.Core.Geometry;
using Glass.Core.Placement;
using Glass.Core.Product;
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

public sealed partial class ControlCenterWindow
{
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
            case "pomodoro":
                WidgetSettingsPanel.Children.Add(SettingNumber(
                    "Focus minutes", "focusMinutes", NumberValue(instance, "focusMinutes", 25), 1, 180));
                WidgetSettingsPanel.Children.Add(SettingNumber(
                    "Short break minutes", "shortBreakMinutes", NumberValue(instance, "shortBreakMinutes", 5), 1, 180));
                WidgetSettingsPanel.Children.Add(SettingNumber(
                    "Long break minutes", "longBreakMinutes", NumberValue(instance, "longBreakMinutes", 15), 1, 180));
                break;
        }
        var save = new Button { Content = "Save instance settings", MinHeight = 36 };
        save.Click += SaveWidgetSettings_Click;
        WidgetSettingsPanel.Children.Add(save);
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

}
