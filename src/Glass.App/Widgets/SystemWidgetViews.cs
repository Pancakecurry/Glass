using System.Globalization;
using Glass.App.Runtime;
using Glass.Widgets.Abstractions;
using Glass.Widgets.BuiltIn;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using static Glass.App.Widgets.WidgetViewPrimitives;

namespace Glass.App.Widgets;

internal sealed class SystemWidgetViews(WidgetViewServices services)
{
    public FrameworkElement Media(WidgetViewMode mode)
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
        panel.Children.Add(Actions(
            AsyncAction("\uE892", "Previous", services.Media.TrySkipPreviousAsync),
            AsyncAction("\uE768", "Play or pause", services.Media.TryTogglePlayPauseAsync),
            AsyncAction("\uE893", "Next", services.Media.TrySkipNextAsync)));
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
            if (snapshot.Artwork is { Length: > 0 }) ObserveArtworkAsync(art, snapshot.Artwork);
        }
        services.Media.StateChanged += OnChanged;
        panel.Unloaded += (_, _) => services.Media.StateChanged -= OnChanged;
        Refresh(services.Media.Current);
        return panel;
        void OnChanged(Glass.Platform.Windows.Media.MediaSessionSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    public FrameworkElement Clock(ClockWidgetInstance instance, WidgetViewMode mode)
    {
        var panel = Panel(mode == WidgetViewMode.Compact ? string.Empty : "Clock");
        var time = Display(string.Empty);
        panel.Children.Add(time);
        if (mode != WidgetViewMode.Compact) panel.Children.Add(Caption(instance.Model.TimeZoneDisplayName));
        void Refresh() => time.Text = instance.Model.FormatTime(CultureInfo.CurrentCulture);
        instance.Updated += OnUpdated;
        panel.Unloaded += (_, _) => instance.Updated -= OnUpdated;
        Refresh();
        return panel;
        void OnUpdated(object? sender, EventArgs args) => panel.DispatcherQueue.TryEnqueue(Refresh);
    }

    public FrameworkElement Date(DateWidgetInstance instance, WidgetViewMode mode)
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

    public FrameworkElement Cpu() => MetricView("CPU", "Processor utilization",
        snapshot => $"{snapshot.CpuPercent:0}%");

    public FrameworkElement Ram() => MetricView("Memory", "Physical memory in use", snapshot =>
        $"{(snapshot.MemoryTotalBytes == 0 ? 0 : snapshot.MemoryUsedBytes * 100d / snapshot.MemoryTotalBytes):0}%");

    public FrameworkElement Network()
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
        BindMetrics(panel, Refresh);
        return panel;
    }

    public FrameworkElement Storage(bool single)
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
        BindMetrics(panel, Refresh);
        return panel;
    }

    public FrameworkElement Battery()
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

    public FrameworkElement Audio()
    {
        var panel = Panel("Audio");
        var endpoint = Caption("No output device");
        var slider = new Slider { Minimum = 0, Maximum = 100, Header = "Volume" };
        var mute = new ToggleButton { Content = "Mute", MinHeight = 36 };
        var settings = new Button { Content = "Sound settings" };
        var updating = false;
        slider.ValueChanged += (_, args) => { if (!updating) services.Audio.SetVolume(args.NewValue / 100); };
        mute.Click += (_, _) => services.Audio.SetMuted(mute.IsChecked == true);
        settings.Click += (_, _) => services.Launcher.OpenSoundSettings();
        panel.Children.Add(endpoint); panel.Children.Add(slider);
        panel.Children.Add(mute); panel.Children.Add(settings);
        void Refresh(Glass.Platform.Windows.Audio.AudioEndpointSnapshot snapshot)
        {
            updating = true;
            endpoint.Text = snapshot.IsAvailable ? snapshot.DisplayName : "No output device";
            slider.IsEnabled = snapshot.IsAvailable;
            slider.Value = snapshot.Volume * 100;
            mute.IsChecked = snapshot.IsMuted;
            updating = false;
        }
        services.Audio.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Audio.Changed -= OnChanged;
        Refresh(services.Audio.Current);
        return panel;
        void OnChanged(Glass.Platform.Windows.Audio.AudioEndpointSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => Refresh(snapshot));
    }

    private FrameworkElement MetricView(string title, string caption,
        Func<Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot, string> format)
    {
        var panel = Metric(title, caption, out var value);
        BindMetrics(panel, snapshot => value.Text = format(snapshot));
        return panel;
    }

    private void BindMetrics(StackPanel panel,
        Action<Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot> refresh)
    {
        services.Metrics.Changed += OnChanged;
        panel.Unloaded += (_, _) => services.Metrics.Changed -= OnChanged;
        if (services.Metrics.Current is { } current) refresh(current);
        void OnChanged(Glass.Platform.Windows.SystemStatus.SystemMetricsSnapshot snapshot) =>
            panel.DispatcherQueue.TryEnqueue(() => refresh(snapshot));
    }

    private static async void ObserveArtworkAsync(Image image, byte[] data)
    {
        try
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
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Media artwork decode failed: {exception.Message}");
        }
    }
}
