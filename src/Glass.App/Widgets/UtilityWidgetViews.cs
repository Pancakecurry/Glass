using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Platform.Windows.Clipboard;
using Glass.Platform.Windows.Pickers;
using Glass.Platform.Windows.Windowing;
using Glass.Widgets.Abstractions;
using Glass.Widgets.BuiltIn;
using Glass.Widgets.BuiltIn.Weather;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static Glass.App.Widgets.WidgetViewPrimitives;

namespace Glass.App.Widgets;

internal sealed class UtilityWidgetViews(WidgetViewServices services)
{
    public FrameworkElement Clipboard()
    {
        var panel = Panel("Clipboard");
        var content = new TextBox
        {
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 72,
        };
        var history = new ListView { MaxHeight = 180, SelectionMode = ListViewSelectionMode.None };
        var status = Caption(string.Empty);
        var refresh = new Button { Content = "Refresh", MinHeight = 36 };
        async Task RefreshAsync()
        {
            try
            {
                content.Text = await services.Clipboard.TryReadTextAsync() ??
                    "Clipboard does not contain text.";
                var snapshot = await services.Clipboard.TryReadHistoryAsync();
                history.Items.Clear();
                foreach (var item in snapshot.TextItems) history.Items.Add(item);
                status.Text = snapshot.Availability switch
                {
                    ClipboardHistoryAvailability.Available =>
                        "Windows clipboard history · not stored by Glass",
                    ClipboardHistoryAvailability.Disabled => "Windows clipboard history is disabled",
                    ClipboardHistoryAvailability.AccessDenied => "Clipboard history access was denied",
                    _ => "Clipboard history is unavailable",
                };
            }
            catch (Exception exception)
            {
                status.Text = "Clipboard is temporarily unavailable";
                System.Diagnostics.Debug.WriteLine($"Clipboard refresh failed: {exception}");
            }
        }
        services.Clipboard.Changed += OnChanged;
        refresh.Click += async (_, _) => await RefreshAsync();
        panel.Loaded += async (_, _) => await RefreshAsync();
        panel.Unloaded += (_, _) => services.Clipboard.Changed -= OnChanged;
        panel.Children.Add(content); panel.Children.Add(refresh);
        panel.Children.Add(status); panel.Children.Add(history);
        return panel;
        void OnChanged(object? sender, EventArgs args) =>
            panel.DispatcherQueue.TryEnqueue(async () => await RefreshAsync());
    }

    public FrameworkElement Shortcuts(ShortcutsWidgetInstance instance, nint ownerWindow)
    {
        var panel = Panel("Shortcuts");
        var list = new StackPanel { Spacing = 4 };
        var addApplication = new Button { Content = "Add application", MinHeight = 36 };
        var addFile = new Button { Content = "Add file", MinHeight = 36 };
        var addFolder = new Button { Content = "Add folder", MinHeight = 36 };
        var picker = new OwnedPickerService(ownerWindow);
        addApplication.Click += (_, _) => ShowApplicationPicker(addApplication);
        addFile.Click += async (_, _) =>
        {
            var path = await picker.PickFileAsync();
            if (path is not null) await instance.AddAsync(new ShortcutEntry(
                Guid.NewGuid(), Path.GetFileName(path), ShortcutTargetKind.File, path));
        };
        addFolder.Click += async (_, _) =>
        {
            var path = await picker.PickFolderAsync();
            if (path is not null) await instance.AddAsync(new ShortcutEntry(
                Guid.NewGuid(), Path.GetFileName(path), ShortcutTargetKind.Folder, path));
        };
        void ShowApplicationPicker(Button anchor)
        {
            var flyout = new MenuFlyout();
            IReadOnlyList<ApplicationDescriptor> applications;
            try { applications = services.Applications.Refresh(); }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Application refresh failed: {exception}");
                applications = services.Applications.Current;
            }
            foreach (var application in applications.Take(80))
            {
                var item = new MenuFlyoutItem { Text = application.DisplayName };
                item.Click += async (_, _) => await instance.AddAsync(new ShortcutEntry(
                    Guid.NewGuid(), application.DisplayName, ShortcutTargetKind.Application,
                    application.Identity.ToString()));
                flyout.Items.Add(item);
            }
            if (flyout.Items.Count == 0)
                flyout.Items.Add(new MenuFlyoutItem { Text = "No applications found", IsEnabled = false });
            flyout.ShowAt(anchor);
        }
        void Refresh()
        {
            list.Children.Clear();
            foreach (var shortcut in instance.Shortcuts.Entries)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                var exists = shortcut.Kind == ShortcutTargetKind.Application ||
                    File.Exists(shortcut.Target) || Directory.Exists(shortcut.Target);
                var icon = new Image { Width = 24, Height = 24, Stretch = Stretch.Uniform };
                var fallback = new FontIcon { Glyph = "\uE8B7", FontSize = 18 };
                var iconHost = new Grid { Width = 24, Height = 24 };
                iconHost.Children.Add(fallback); iconHost.Children.Add(icon);
                var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                content.Children.Add(iconHost);
                content.Children.Add(new TextBlock
                {
                    Text = exists ? shortcut.DisplayName : $"{shortcut.DisplayName} — missing",
                    VerticalAlignment = VerticalAlignment.Center,
                });
                var open = new Button { Content = content, MinHeight = 36, IsEnabled = exists };
                open.Click += (_, _) => OpenShortcut(shortcut);
                ObserveShortcutIconAsync(shortcut, icon, fallback, ownerWindow);
                var remove = GlyphButton("\uE74D", $"Remove {shortcut.DisplayName}");
                remove.Click += async (_, _) => await instance.RemoveAsync(shortcut.ShortcutId);
                row.Children.Add(open); row.Children.Add(remove); list.Children.Add(row);
            }
        }
        instance.Updated += OnUpdated;
        panel.Unloaded += (_, _) => instance.Updated -= OnUpdated;
        panel.Children.Add(list); panel.Children.Add(Actions(addApplication, addFile, addFolder));
        Refresh();
        return panel;
        void OnUpdated(object? sender, EventArgs args) => panel.DispatcherQueue.TryEnqueue(Refresh);
    }

    public FrameworkElement Weather(WidgetInstanceConfiguration configuration)
    {
        var panel = Panel("Weather");
        var location = new TextBox
        {
            Header = "Location",
            Text = configuration.Settings.GetValueOrDefault("locationLabel") ?? "Current location",
        };
        var latitude = new NumberBox
        {
            Header = "Latitude", Value = ReadNumber(configuration.Settings, "latitude", double.NaN),
            Minimum = -90, Maximum = 90,
        };
        var longitude = new NumberBox
        {
            Header = "Longitude", Value = ReadNumber(configuration.Settings, "longitude", double.NaN),
            Minimum = -180, Maximum = 180,
        };
        var conditions = Display("Configure a location");
        var range = Value(string.Empty);
        var forecast = Caption(string.Empty);
        var detail = Caption("Weather data: MET Norway (CC BY 4.0)");
        var load = new Button { Content = "Update", MinHeight = 36 };
        var device = new Button { Content = "Use device location", MinHeight = 36 };
        load.Click += async (_, _) => await LoadAsync();
        device.Click += async (_, _) =>
        {
            var result = await services.Location.RequestAsync();
            if (result.Access == Glass.Platform.Windows.Location.LocationAccessResult.Granted)
            {
                latitude.Value = result.Latitude;
                longitude.Value = result.Longitude;
                await LoadAsync();
            }
            else detail.Text = "Location was not granted. Enter coordinates instead.";
        };
        async Task LoadAsync()
        {
            if (double.IsNaN(latitude.Value) || double.IsNaN(longitude.Value)) return;
            conditions.Text = "Updating…";
            var snapshot = await services.Weather.GetAsync(new WeatherLocation(
                latitude.Value, longitude.Value, location.Text));
            conditions.Text = snapshot is null ? "Weather unavailable" : $"{snapshot.AirTemperatureCelsius:0} °C";
            range.Text = snapshot is { HighTemperatureCelsius: { } high, LowTemperatureCelsius: { } low }
                ? $"High {high:0}° · Low {low:0}°" : string.Empty;
            forecast.Text = snapshot is null ? string.Empty : string.Join("   ", snapshot.Forecast.Select(point =>
                $"{point.At:HH:mm}  {point.AirTemperatureCelsius:0}°  {point.SymbolCode.Replace('_', ' ')}"));
            detail.Text = snapshot is null ? "Check the connection or location" :
                $"{snapshot.SymbolCode.Replace('_', ' ')}{(snapshot.IsStale ? " · cached" : string.Empty)} · {snapshot.Attribution}";
        }
        panel.Children.Add(conditions); panel.Children.Add(range); panel.Children.Add(forecast);
        panel.Children.Add(detail); panel.Children.Add(location);
        panel.Children.Add(Actions(latitude, longitude)); panel.Children.Add(Actions(load, device));
        return panel;
    }

    private void OpenShortcut(ShortcutEntry shortcut)
    {
        if (shortcut.Kind != ShortcutTargetKind.Application)
        {
            services.Launcher.OpenPath(shortcut.Target);
            return;
        }
        if (TryParseIdentity(shortcut.Target, out var identity)) services.Launcher.Launch(identity);
    }

    private async void ObserveShortcutIconAsync(
        ShortcutEntry shortcut, Image image, FontIcon fallback, nint ownerWindow)
    {
        try
        {
            var identity = shortcut.Kind == ShortcutTargetKind.Application &&
                TryParseIdentity(shortcut.Target, out var parsed)
                    ? parsed
                    : new ApplicationIdentity(ApplicationIdentityKind.CanonicalExecutablePath, shortcut.Target);
            using var bitmap = services.Icons.GetBitmap(identity, 24, WindowPositioner.GetDpi(ownerWindow));
            if (bitmap is null) return;
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(bitmap);
            image.Source = source;
            fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Shortcut icon load failed: {exception.Message}");
        }
    }
}
