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

}
