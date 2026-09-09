using System.Globalization;
using Glass.App.Runtime;
using Glass.Core.Applications;
using Glass.Platform.Windows.Clipboard;
using Glass.Platform.Windows.Pickers;
using Glass.Platform.Windows.Windowing;
using Glass.Widgets.Abstractions;
using Glass.Widgets.BuiltIn;
using Glass.Widgets.BuiltIn.Weather;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Glass.App.Widgets;

internal sealed class BuiltInWidgetViewFactory(WidgetViewServices services)
{
    public FrameworkElement Create(IWidgetInstance instance, nint ownerWindow)
    {
        var mode = WidgetViewModeSelector.Select(
            instance.Configuration.TypeId, instance.Configuration.Size);
        var content = instance.Configuration.TypeId.Value switch
        {
            "media" => Media(mode),
            "clock" => Clock((ClockWidgetInstance)instance, mode),
            "date" => Date((DateWidgetInstance)instance, mode),
            "cpu" => Cpu(),
            "ram" => Ram(),
            "network" => Network(),
            "storage" => Storage(single: true),
            "battery" => Battery(),
            "audio" => Audio(),
            "calendar" => Calendar((CalendarWidgetInstance)instance),
            "timer" => Timer((TimerWidgetInstance)instance),
            "stopwatch" => Stopwatch((StopwatchWidgetInstance)instance),
            "calculator" => Calculator((CalculatorWidgetInstance)instance),
            "notes" => Notes((NotesWidgetInstance)instance),
            "clipboard" => Clipboard(ownerWindow),
            "storageUtility" => Storage(single: false),
            "shortcuts" => Shortcuts((ShortcutsWidgetInstance)instance, ownerWindow),
            "weather" => Weather(instance.Configuration),
            "pomodoro" => Pomodoro((PomodoroWidgetInstance)instance),
            _ => Unavailable("Widget unavailable", "This built-in widget type is not registered."),
        };
        AutomationProperties.SetName(content,
            BuiltInWidgetCatalog.All.First(item => item.TypeId == instance.Configuration.TypeId)
                .DisplayName);
        if (content is StackPanel panel)
            panel.Padding = new Thickness(services.Appearance().WidgetDensity switch
            {
                Glass.Core.Appearance.WidgetSurfaceDensity.Compact => 8,
                Glass.Core.Appearance.WidgetSurfaceDensity.Spacious => 16,
                _ => 12,
            });
        return content;
    }

    private FrameworkElement Media(WidgetViewMode mode)
    {
        var panel = Panel("Media");
        var art = new Image
        {
            Width = mode == WidgetViewMode.Expanded ? 112 : 54,
            Height = mode == WidgetViewMode.Expanded ? 112 : 54,
            Stretch = Stretch.UniformToFill,
        };
        var title = Value("Nothing playing");
        var artist = Caption("Start media in a supported Windows application");
        var metadata = new StackPanel { Spacing = 2 };
        metadata.Children.Add(title);
        metadata.Children.Add(artist);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(art);
        row.Children.Add(metadata);
        panel.Children.Add(row);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 6,
        };
        controls.Children.Add(ActionButton("\uE892", "Previous", async () =>
            await services.Media.TrySkipPreviousAsync()));
        controls.Children.Add(ActionButton("\uE768", "Play or pause", async () =>
            await services.Media.TryTogglePlayPauseAsync()));
        controls.Children.Add(ActionButton("\uE893", "Next", async () =>
            await services.Media.TrySkipNextAsync()));
        panel.Children.Add(controls);
        var timeline = new Slider { Minimum = 0, Maximum = 1, IsEnabled = false };
        if (mode != WidgetViewMode.Compact)
        {
            timeline.AddHandler(UIElement.PointerReleasedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(async (_, _) =>
                {
                    if (timeline.Maximum > 0)
                        await services.Media.TrySeekAsync(TimeSpan.FromSeconds(timeline.Value));
                }), true);
            panel.Children.Add(timeline);
        }

        void Refresh(Glass.Platform.Windows.Media.MediaSessionSnapshot snapshot)
        {
            title.Text = snapshot.IsAvailable ? snapshot.Title : "Nothing playing";
            artist.Text = snapshot.IsAvailable
                ? string.Join(" · ", new[] { snapshot.Artist, snapshot.AlbumTitle }
                    .Where(value => !string.IsNullOrWhiteSpace(value)))
                : snapshot.Status;
            timeline.IsEnabled = snapshot.CanSeek;
            timeline.Maximum = Math.Max(1, snapshot.Duration.TotalSeconds);
            timeline.Value = Math.Clamp(snapshot.Position.TotalSeconds, 0, timeline.Maximum);
            if (snapshot.Artwork is { Length: > 0 }) _ = SetImageAsync(art, snapshot.Artwork);
        }
        services.Media.StateChanged += OnChanged;
        panel.Unloaded += (_, _) => services.Media.StateChanged -= OnChanged;
        Refresh(services.Media.Current);
        return panel;
        void OnChanged(Glass.Platform.Windows.Media.MediaSessionSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private static FrameworkElement Clock(ClockWidgetInstance instance, WidgetViewMode mode)
    {
        var panel = Panel(mode == WidgetViewMode.Compact ? string.Empty : "Clock");
        var time = Display(string.Empty);
        var zone = Caption(instance.Model.TimeZoneDisplayName);
        panel.Children.Add(time);
        if (mode != WidgetViewMode.Compact) panel.Children.Add(zone);
        void Refresh() => time.Text = instance.Model.FormatTime(CultureInfo.CurrentCulture);
        instance.Updated += OnUpdated;
        panel.Unloaded += (_, _) => instance.Updated -= OnUpdated;
        Refresh();
        return panel;
        void OnUpdated(object? sender, EventArgs args) =>
            panel.DispatcherQueue.TryEnqueue(Refresh);
    }

    private static FrameworkElement Date(DateWidgetInstance instance, WidgetViewMode mode)
    {
        var panel = Panel(mode == WidgetViewMode.Compact ? string.Empty : "Date");
        var day = Display(string.Empty);
        var detail = Caption(string.Empty);
        panel.Children.Add(day);
        if (mode != WidgetViewMode.Compact) panel.Children.Add(detail);
        void Refresh()
        {
            var now = instance.Model.Current;
            day.Text = now.ToString(mode == WidgetViewMode.Compact ? "ddd, MMM d" : "dddd",
                CultureInfo.CurrentCulture);
            detail.Text = now.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
        }
        instance.Updated += OnUpdated;
        panel.Unloaded += (_, _) => instance.Updated -= OnUpdated;
        Refresh();
        return panel;
        void OnUpdated(object? sender, EventArgs args) => panel.DispatcherQueue.TryEnqueue(Refresh);
    }

    private FrameworkElement Cpu()
    {
        var panel = Metric("CPU", "Processor utilization", out var value);
        void Refresh(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot) =>
            value.Text = $"{snapshot.CpuPercent:0}%";
        services.Metrics.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Metrics.Changed -= OnChanged;
        if (services.Metrics.Current is { } current) Refresh(current);
        return panel;
        void OnChanged(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private FrameworkElement Ram()
    {
        var panel = Metric("Memory", "Physical memory in use", out var value);
        void Refresh(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot)
        {
            var fraction = snapshot.MemoryTotalBytes == 0 ? 0 :
                snapshot.MemoryUsedBytes * 100d / snapshot.MemoryTotalBytes;
            value.Text = $"{fraction:0}%";
        }
        services.Metrics.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Metrics.Changed -= OnChanged;
        if (services.Metrics.Current is { } current) Refresh(current);
        return panel;
        void OnChanged(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private FrameworkElement Network()
    {
        var panel = Panel("Network");
        var down = Value("↓ 0 B/s");
        var up = Value("↑ 0 B/s");
        panel.Children.Add(down);
        panel.Children.Add(up);
        void Refresh(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot)
        {
            down.Text = $"↓ {FormatRate(snapshot.NetworkReceivedBytesPerSecond)}";
            up.Text = $"↑ {FormatRate(snapshot.NetworkSentBytesPerSecond)}";
        }
        services.Metrics.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Metrics.Changed -= OnChanged;
        if (services.Metrics.Current is { } current) Refresh(current);
        return panel;
        void OnChanged(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private FrameworkElement Storage(bool single)
    {
        var panel = Panel(single ? "Storage" : "Storage Utility");
        void Refresh(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot)
        {
            while (panel.Children.Count > 1) panel.Children.RemoveAt(1);
            foreach (var volume in snapshot.Volumes.Take(single ? 1 : 6))
            {
                var used = volume.TotalBytes == 0 ? 0 :
                    (volume.TotalBytes - volume.FreeBytes) * 100d / volume.TotalBytes;
                var button = new Button
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Content = $"{(string.IsNullOrWhiteSpace(volume.Name) ? volume.RootPath : volume.Name)}  {used:0}% used",
                };
                button.Click += (_, _) => services.Launcher.OpenPath(volume.RootPath);
                AutomationProperties.SetName(button, $"Open volume {volume.RootPath}");
                panel.Children.Add(button);
            }
        }
        services.Metrics.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Metrics.Changed -= OnChanged;
        if (services.Metrics.Current is { } current) Refresh(current);
        return panel;
        void OnChanged(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private FrameworkElement Battery()
    {
        var panel = Metric("Battery", "Power status", out var value);
        var state = Caption(string.Empty);
        panel.Children.Add(state);
        void Refresh(Glass.Platform.Windows.SystemStatus.PowerSnapshot snapshot)
        {
            value.Text = snapshot.RemainingChargePercent is { } percent ? $"{percent}%" : "No battery";
            state.Text = snapshot.PowerSupplyStatus.ToString();
        }
        services.Power.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Power.Changed -= OnChanged;
        Refresh(services.Power.Current);
        return panel;
        void OnChanged(Glass.Platform.Windows.SystemStatus.PowerSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private FrameworkElement Audio()
    {
        var panel = Panel("Audio");
        var endpoint = Caption("No output device");
        var slider = new Slider { Minimum = 0, Maximum = 100, Header = "Volume" };
        var mute = new ToggleButton { Content = "Mute", MinHeight = 36 };
        var settings = new Button { Content = "Sound settings" };
        slider.ValueChanged += (_, args) => services.Audio.SetVolume(args.NewValue / 100);
        mute.Click += (_, _) => services.Audio.SetMuted(mute.IsChecked == true);
        settings.Click += (_, _) => services.Launcher.OpenSoundSettings();
        panel.Children.Add(endpoint);
        panel.Children.Add(slider);
        panel.Children.Add(mute);
        panel.Children.Add(settings);
        void Refresh(Glass.Platform.Windows.Audio.AudioEndpointSnapshot snapshot)
        {
            endpoint.Text = snapshot.IsAvailable ? snapshot.DisplayName : "No output device";
            slider.IsEnabled = snapshot.IsAvailable;
            slider.Value = snapshot.Volume * 100;
            mute.IsChecked = snapshot.IsMuted;
        }
        services.Audio.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Audio.Changed -= OnChanged;
        Refresh(services.Audio.Current);
        return panel;
        void OnChanged(Glass.Platform.Windows.Audio.AudioEndpointSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private static FrameworkElement Calendar(CalendarWidgetInstance instance)
    {
        var panel = Panel("Calendar");
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var month = Value(string.Empty);
        var previous = Button("\uE76B", "Previous month");
        var next = Button("\uE76C", "Next month");
        var today = new Button { Content = "Today", MinHeight = 36 };
        previous.Click += (_, _) => instance.Previous();
        next.Click += (_, _) => instance.Next();
        today.Click += (_, _) => instance.Today();
        header.Children.Add(previous);
        header.Children.Add(month);
        header.Children.Add(next);
        header.Children.Add(today);
        var grid = new Grid();
        var selectedDate = Caption("No date selected");
        for (var i = 0; i < 7; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 6; i++) grid.RowDefinitions.Add(new RowDefinition());
        panel.Children.Add(header);
        panel.Children.Add(selectedDate);
        panel.Children.Add(grid);
        void Refresh()
        {
            month.Text = instance.Model.DisplayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            selectedDate.Text = instance.Model.SelectedDate is { } selected
                ? selected.ToString("D", CultureInfo.CurrentCulture)
                : "No date selected";
            grid.Children.Clear();
            var first = instance.Model.DisplayedMonth;
            var offset = ((int)first.DayOfWeek - (int)instance.Model.FirstDayOfWeek + 7) % 7;
            for (var day = 1; day <= DateTime.DaysInMonth(first.Year, first.Month); day++)
            {
                var date = new DateOnly(first.Year, first.Month, day);
                var button = new Button
                {
                    Content = instance.Model.SelectedDate == date
                        ? $"{day} selected"
                        : day.ToString(CultureInfo.CurrentCulture),
                    MinWidth = 32,
                    MinHeight = 32,
                    Padding = new Thickness(2),
                };
                AutomationProperties.SetName(button,
                    instance.Model.SelectedDate == date
                        ? $"{date:D}, selected"
                        : date.ToString("D", CultureInfo.CurrentCulture));
                button.Click += (_, _) => instance.Select(date);
                Grid.SetColumn(button, (offset + day - 1) % 7);
                Grid.SetRow(button, (offset + day - 1) / 7);
                grid.Children.Add(button);
            }
        }
        instance.Updated += OnUpdated;
        panel.Unloaded += (_, _) => instance.Updated -= OnUpdated;
        Refresh();
        return panel;
        void OnUpdated(object? sender, EventArgs args) => panel.DispatcherQueue.TryEnqueue(Refresh);
    }

    private static FrameworkElement Timer(TimerWidgetInstance instance)
    {
        var panel = Panel("Timer");
        var remaining = Display("05:00");
        var duration = new NumberBox
        {
            Header = "Minutes",
            Value = ReadNumber(instance.Configuration.Settings, "durationMinutes", 5),
            Minimum = 1,
            Maximum = 1440,
        };
        var controls = Actions(
            TextAction("Start", () => instance.Start(TimeSpan.FromMinutes(duration.Value))),
            TextAction("Pause", instance.Pause), TextAction("Resume", instance.Resume),
            TextAction("Reset", instance.Reset));
        panel.Children.Add(remaining);
        panel.Children.Add(duration);
        panel.Children.Add(controls);
        return WithTimer(panel, () => remaining.Text = FormatTime(instance.Timer.Remaining));
    }

    private static FrameworkElement Stopwatch(StopwatchWidgetInstance instance)
    {
        var panel = Panel("Stopwatch");
        var elapsed = Display("00:00.0");
        var laps = Caption("No laps");
        panel.Children.Add(elapsed);
        panel.Children.Add(Actions(TextAction("Start", instance.Start),
            TextAction("Pause", instance.Pause), TextAction("Lap", instance.Lap),
            TextAction("Reset", instance.Reset)));
        panel.Children.Add(laps);
        return WithTimer(panel, () =>
        {
            elapsed.Text = FormatTime(instance.Stopwatch.Elapsed, tenths: true);
            laps.Text = instance.Stopwatch.State.Laps.Count == 0 ? "No laps" :
                string.Join("  ", instance.Stopwatch.State.Laps.TakeLast(4)
                    .Select(value => FormatTime(value)));
        });
    }

    private static FrameworkElement Calculator(CalculatorWidgetInstance instance)
    {
        var panel = Panel("Calculator");
        var expression = new TextBox { PlaceholderText = "Expression", MinHeight = 40 };
        var result = Display("0");
        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 4 };
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var keys = new[] { "7", "8", "9", "/", "4", "5", "6", "*", "1", "2", "3", "-", "0", ".", "%", "+" };
        for (var i = 0; i < keys.Length; i++)
        {
            if (i % 4 == 0) grid.RowDefinitions.Add(new RowDefinition());
            var key = keys[i];
            var button = new Button { Content = key, MinHeight = 38, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => { expression.Text += key; instance.Append(key); };
            Grid.SetColumn(button, i % 4);
            Grid.SetRow(button, i / 4);
            grid.Children.Add(button);
        }
        void Evaluate()
        {
            instance.Clear();
            instance.Append(expression.Text);
            instance.Evaluate();
            result.Text = instance.Result;
        }
        var evaluate = TextAction("Equals", Evaluate);
        expression.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter) Evaluate();
        };
        panel.Children.Add(expression);
        panel.Children.Add(result);
        panel.Children.Add(grid);
        panel.Children.Add(Actions(TextAction("Clear", () => { expression.Text = string.Empty; instance.Clear(); }), evaluate));
        return panel;
    }

    private static FrameworkElement Notes(NotesWidgetInstance instance)
    {
        var panel = Panel("Quick Notes");
        var editor = new TextBox
        {
            Text = instance.Model.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            PlaceholderText = "Write a note…",
        };
        var status = Caption("Saved locally");
        editor.TextChanged += (_, _) => { instance.Update(editor.Text); status.Text = "Saving locally…"; };
        editor.LostFocus += (_, _) => status.Text = "Saved locally";
        panel.Children.Add(editor);
        panel.Children.Add(status);
        return panel;
    }

    private FrameworkElement Clipboard(nint ownerWindow)
    {
        var panel = Panel("Clipboard");
        var content = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 72 };
        var history = new ListView { MaxHeight = 180, SelectionMode = ListViewSelectionMode.None };
        var status = Caption(string.Empty);
        var refresh = new Button { Content = "Refresh", MinHeight = 36 };
        var router = new NativeWindowMessageRouter(ownerWindow);
        var clipboard = new ClipboardService(ownerWindow, router);
        async Task RefreshAsync()
        {
            content.Text = await clipboard.TryReadTextAsync() ?? "Clipboard does not contain text.";
            var snapshot = await clipboard.TryReadHistoryAsync();
            history.Items.Clear();
            foreach (var item in snapshot.TextItems) history.Items.Add(item);
            status.Text = snapshot.Availability switch
            {
                ClipboardHistoryAvailability.Available => "Windows clipboard history · not stored by Glass",
                ClipboardHistoryAvailability.Disabled => "Windows clipboard history is disabled",
                ClipboardHistoryAvailability.AccessDenied => "Clipboard history access was denied",
                _ => "Clipboard history is unavailable",
            };
        }
        clipboard.Changed += OnChanged;
        refresh.Click += async (_, _) => await RefreshAsync();
        panel.Loaded += async (_, _) => await RefreshAsync();
        panel.Unloaded += (_, _) => { clipboard.Changed -= OnChanged; clipboard.Dispose(); router.Dispose(); };
        panel.Children.Add(content);
        panel.Children.Add(refresh);
        panel.Children.Add(status);
        panel.Children.Add(history);
        return panel;
        void OnChanged(object? sender, EventArgs args) => panel.DispatcherQueue.TryEnqueue(async () => await RefreshAsync());
    }

    private FrameworkElement Shortcuts(ShortcutsWidgetInstance instance, nint ownerWindow)
    {
        var panel = Panel("Shortcuts");
        var list = new StackPanel { Spacing = 4 };
        var addApplication = new Button { Content = "Add application", MinHeight = 36 };
        var addFile = new Button { Content = "Add file", MinHeight = 36 };
        var addFolder = new Button { Content = "Add folder", MinHeight = 36 };
        var picker = new OwnedPickerService(ownerWindow);
        addApplication.Click += (_, _) =>
        {
            var flyout = new MenuFlyout();
            IReadOnlyList<ApplicationDescriptor> applications;
            try { applications = services.Applications.Refresh(); }
            catch { applications = services.Applications.Current; }
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
            flyout.ShowAt(addApplication);
        };
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
                iconHost.Children.Add(fallback);
                iconHost.Children.Add(icon);
                var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                content.Children.Add(iconHost);
                content.Children.Add(new TextBlock
                {
                    Text = exists ? shortcut.DisplayName : $"{shortcut.DisplayName} — missing",
                    VerticalAlignment = VerticalAlignment.Center,
                });
                var open = new Button { Content = content, MinHeight = 36 };
                open.IsEnabled = exists;
                open.Click += (_, _) => OpenShortcut(shortcut);
                _ = LoadShortcutIconAsync(shortcut, icon, fallback);
                var remove = Button("\uE74D", $"Remove {shortcut.DisplayName}");
                remove.Click += async (_, _) => await instance.RemoveAsync(shortcut.ShortcutId);
                row.Children.Add(open);
                row.Children.Add(remove);
                list.Children.Add(row);
            }
        }
        instance.Updated += OnUpdated;
        panel.Unloaded += (_, _) => instance.Updated -= OnUpdated;
        panel.Children.Add(list);
        panel.Children.Add(Actions(addApplication, addFile, addFolder));
        Refresh();
        return panel;
        void OnUpdated(object? sender, EventArgs args) => panel.DispatcherQueue.TryEnqueue(Refresh);

        void OpenShortcut(ShortcutEntry shortcut)
        {
            if (shortcut.Kind != ShortcutTargetKind.Application)
            {
                services.Launcher.OpenPath(shortcut.Target);
                return;
            }
            var separator = shortcut.Target.IndexOf(':');
            if (separator <= 0 || !Enum.TryParse<ApplicationIdentityKind>(
                shortcut.Target[..separator], out var kind)) return;
            services.Launcher.Launch(new ApplicationIdentity(kind, shortcut.Target[(separator + 1)..]));
        }

        async Task LoadShortcutIconAsync(ShortcutEntry shortcut, Image image, FontIcon fallback)
        {
            try
            {
                var identity = shortcut.Kind == ShortcutTargetKind.Application &&
                    TryParseIdentity(shortcut.Target, out var parsed)
                        ? parsed
                        : new ApplicationIdentity(
                            ApplicationIdentityKind.CanonicalExecutablePath, shortcut.Target);
                using var bitmap = services.Icons.GetBitmap(
                    identity, 24, WindowPositioner.GetDpi(ownerWindow));
                if (bitmap is null) return;
                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(bitmap);
                image.Source = source;
                fallback.Visibility = Visibility.Collapsed;
            }
            catch { }
        }
    }

    private FrameworkElement Weather(WidgetInstanceConfiguration configuration)
    {
        var panel = Panel("Weather");
        var location = new TextBox { Header = "Location", Text = configuration.Settings.GetValueOrDefault("locationLabel") ?? "Current location" };
        var latitude = new NumberBox
        {
            Header = "Latitude",
            Value = ReadNumber(configuration.Settings, "latitude", double.NaN),
            Minimum = -90,
            Maximum = 90,
        };
        var longitude = new NumberBox
        {
            Header = "Longitude",
            Value = ReadNumber(configuration.Settings, "longitude", double.NaN),
            Minimum = -180,
            Maximum = 180,
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
        panel.Children.Add(conditions);
        panel.Children.Add(range);
        panel.Children.Add(forecast);
        panel.Children.Add(detail);
        panel.Children.Add(location);
        panel.Children.Add(Actions(latitude, longitude));
        panel.Children.Add(Actions(load, device));
        return panel;
    }

    private static FrameworkElement Pomodoro(PomodoroWidgetInstance instance)
    {
        var panel = Panel("Pomodoro");
        var phase = Value(instance.Pomodoro.Phase.ToString());
        var remaining = Display("25:00");
        panel.Children.Add(phase);
        panel.Children.Add(remaining);
        panel.Children.Add(Actions(TextAction("Start", instance.Start),
            TextAction("Pause", instance.Pause), TextAction("Reset", instance.Reset),
            TextAction("Next phase", instance.Advance)));
        return WithTimer(panel, () =>
        {
            phase.Text = $"{instance.Pomodoro.Phase} · {instance.Pomodoro.CompletedFocusIntervals} completed";
            remaining.Text = FormatTime(instance.Pomodoro.Timer.Remaining);
        });
    }

    private static StackPanel Panel(string title)
    {
        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(12) };
        if (!string.IsNullOrEmpty(title)) panel.Children.Add(new TextBlock
        {
            Text = title,
            Style = Application.Current.Resources["GlassSectionTitleStyle"] as Style,
        });
        return panel;
    }

    private static StackPanel Metric(string title, string caption, out TextBlock value)
    {
        var panel = Panel(title);
        value = Display("—");
        panel.Children.Add(value);
        panel.Children.Add(Caption(caption));
        return panel;
    }

    private static TextBlock Value(string text) => new()
    {
        Text = text,
        FontSize = 16,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    private static TextBlock Display(string text) => new()
    {
        Text = text,
        FontSize = 30,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.72,
        FontSize = 12,
    };
    private static Button Button(string glyph, string name)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph },
            MinWidth = 36,
            MinHeight = 36,
            Padding = new Thickness(6),
        };
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        return button;
    }
    private static Button ActionButton(string glyph, string name, Func<Task> action)
    {
        var button = Button(glyph, name);
        button.Click += async (_, _) => await action();
        return button;
    }
    private static Button TextAction(string text, Action action)
    {
        var button = new Button { Content = text, MinHeight = 36 };
        button.Click += (_, _) => action();
        return button;
    }
    private static StackPanel Actions(params UIElement[] controls)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var control in controls) panel.Children.Add(control);
        return panel;
    }
    private static StackPanel WithTimer(StackPanel panel, Action refresh)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => refresh();
        panel.Loaded += (_, _) => { refresh(); timer.Start(); };
        panel.Unloaded += (_, _) => timer.Stop();
        return panel;
    }
    private static FrameworkElement Unavailable(string title, string detail)
    {
        var panel = Panel(title);
        panel.Children.Add(Caption(detail));
        return panel;
    }
    private static string FormatTime(TimeSpan value, bool tenths = false) => tenths
        ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 100}"
        : $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    private static string FormatRate(long bytes) => bytes switch
    {
        >= 1_000_000 => $"{bytes / 1_000_000d:0.0} MB/s",
        >= 1_000 => $"{bytes / 1_000d:0.0} KB/s",
        _ => $"{bytes} B/s",
    };

    private static double ReadNumber(
        IReadOnlyDictionary<string, string> settings,
        string key,
        double fallback) =>
        settings.TryGetValue(key, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;

    private static bool TryParseIdentity(string value, out ApplicationIdentity identity)
    {
        var separator = value.IndexOf(':');
        if (separator > 0 && Enum.TryParse<ApplicationIdentityKind>(
            value[..separator], out var kind))
        {
            identity = new ApplicationIdentity(kind, value[(separator + 1)..]);
            return identity.IsValid;
        }
        identity = default;
        return false;
    }
    private static async Task SetImageAsync(Image image, byte[] data)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(data);
            await writer.StoreAsync();
        }
        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        image.Source = bitmap;
    }
}
