using Glass.Core.Appearance;
using Glass.Core.Product;
using Glass.Platform.Windows.Runtime;
using Glass.Platform.Windows.Pickers;
using Glass.Platform.Windows.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Glass.App;

public sealed partial class ControlCenterWindow
{
    private async Task LoadAdvancedAsync()
    {
        _loading = true;
        VersionText.Text = $"Glass {ProductVersion.Current.Informational}";
        Select(RenderingQuality, _settings.Current.Behavior.RenderingQuality.ToString());
        var startup = await _startupTask.GetAsync();
        StartWithWindows.IsEnabled = startup.Availability == StartupTaskAvailability.Available;
        StartWithWindows.IsOn = startup.IsEnabled;
        StartupStatus.Text = startup.Availability switch
        {
            StartupTaskAvailability.Available => "Starts quietly after Windows sign-in.",
            StartupTaskAvailability.DisabledByPolicy => "Disabled by Windows or your organization.",
            _ => "Available in packaged installations only.",
        };
        DiagnosticsText.Text = _diagnosticsSnapshot();
        _loading = false;
    }

    private async Task LoadAdvancedObservedAsync()
    {
        try { await LoadAdvancedAsync(); }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async void RenderingQuality_SelectionChanged(
        object sender, SelectionChangedEventArgs args)
    {
        if (_loading) return;
        await _settings.UpdateAsync(current => current with
        {
            Behavior = current.Behavior with
            {
                RenderingQuality = Parse(RenderingQuality, RenderingQualityPreference.Auto),
            },
        });
    }

    private async void StartWithWindows_Toggled(object sender, RoutedEventArgs args)
    {
        if (_loading || !StartWithWindows.IsEnabled) return;
        var result = await _startupTask.SetEnabledAsync(StartWithWindows.IsOn);
        await _settings.UpdateAsync(current => current with
        {
            Behavior = current.Behavior with { StartWithWindows = result.IsEnabled },
        });
        await LoadAdvancedAsync();
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(_diagnosticsSnapshot());
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        StatusText.Text = "Diagnostics copied without personal widget content";
    }

    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            var picker = new OwnedPickerService(WinUiWindowHandle.FromWindow(this).Hwnd);
            if (await picker.SaveTextAsync("Glass-diagnostics", _diagnosticsSnapshot()))
                StatusText.Text = "Redacted diagnostics exported";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Diagnostics export cancelled";
        }
    }

    private async void Restart_Click(object sender, RoutedEventArgs args) =>
        await _restart(false);

    private async void RestartSafeMode_Click(object sender, RoutedEventArgs args) =>
        await _restart(true);

    private async void ResetConfiguration_Click(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = SurfaceChrome.XamlRoot,
            Title = "Reset Glass configuration?",
            Content = "Bars, appearance, and behavior will return to defaults after a backup. Quick Notes and other personal widget contents are not deleted.",
            PrimaryButtonText = "Reset and restart",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await _resetConfiguration();
    }

    private async void Exit_Click(object sender, RoutedEventArgs args) => await _shutdown();
}
