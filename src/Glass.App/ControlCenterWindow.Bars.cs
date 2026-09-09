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
    private async void ContentToStart_Click(object sender, RoutedEventArgs args) =>
        await MoveSelectedContentToZoneAsync(BarZone.Start);
    private async void ContentToCenter_Click(object sender, RoutedEventArgs args) =>
        await MoveSelectedContentToZoneAsync(BarZone.Center);
    private async void ContentToEnd_Click(object sender, RoutedEventArgs args) =>
        await MoveSelectedContentToZoneAsync(BarZone.End);
    private async Task MoveSelectedContentAsync(int direction)
    {
        if (SelectedBar is not { } bar || BarContentList.SelectedIndex < 0) return;
        var from = BarContentList.SelectedIndex;
        var to = Math.Clamp(from + direction, 0, bar.Content.Count - 1);
        await RunAsync(async () => await _shell.MoveBarContentAsync(bar.Id, from, to));
    }
    private async Task MoveSelectedContentToZoneAsync(BarZone zone)
    {
        if (SelectedBar is not { } bar || BarContentList.SelectedIndex < 0) return;
        var index = BarContentList.SelectedIndex;
        await RunAsync(async () => await _shell.MoveBarContentAsync(bar.Id, index, index, zone));
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

}
