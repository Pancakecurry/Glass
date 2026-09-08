using Glass.App.Configuration;
using Glass.Core.Placement;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Media;
using Glass.Platform.Windows.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glass.App;

public sealed partial class TechnicalSpikeWindow : Window, IDisposable
{
    private readonly WindowsDisplayService _displayService;
    private readonly SystemMediaSessionService _mediaService;
    private ProbeBarWindow? _probeBar;
    private bool _disposed;

    public TechnicalSpikeWindow()
    {
        InitializeComponent();
        Title = ProductBranding.TechnicalSpikeWindowTitle;

        _displayService = new WindowsDisplayService();
        _displayService.DisplaysChanged += OnDisplaysChanged;
        _mediaService = new SystemMediaSessionService();
        _mediaService.StateChanged += OnMediaStateChanged;
        Closed += OnClosed;

        PopulateDisplays();
        UpdateMediaStatus(_mediaService.Current);
        _ = InitializeMediaAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Closed -= OnClosed;
        _displayService.DisplaysChanged -= OnDisplaysChanged;
        _mediaService.StateChanged -= OnMediaStateChanged;

        if (_probeBar is not null)
        {
            _probeBar.StateChanged -= OnProbeStateChanged;
            _probeBar.Dispose();
            _probeBar.Close();
            _probeBar = null;
        }

        _mediaService.Dispose();
        _displayService.Dispose();
    }

    private DisplayInfo SelectedDisplay =>
        DisplaySelector.SelectedItem is ComboBoxItem { Tag: DisplayInfo display }
            ? display
            : _displayService.PrimaryDisplay;

    private ProbeBarWindow EnsureProbeBar()
    {
        if (_probeBar is not null)
        {
            return _probeBar;
        }

        _probeBar = new ProbeBarWindow();
        _probeBar.StateChanged += OnProbeStateChanged;
        _probeBar.Closed += OnProbeClosed;
        _probeBar.UpdateMedia(_mediaService.Current);
        _probeBar.ShowSurface();
        _probeBar.CenterOn(SelectedDisplay);
        UpdateSurfaceStatus();
        return _probeBar;
    }

    private void PopulateDisplays()
    {
        var selectedId =
            (DisplaySelector.SelectedItem as ComboBoxItem)?.Tag is DisplayInfo selected
                ? selected.Id
                : _displayService.PrimaryDisplay.Id;

        DisplaySelector.Items.Clear();
        foreach (var display in _displayService.Displays)
        {
            DisplaySelector.Items.Add(new ComboBoxItem
            {
                Content = display.IsPrimary
                    ? $"{display.Name} (Primary)"
                    : display.Name,
                Tag = display,
            });
        }

        DisplaySelector.SelectedItem = DisplaySelector.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item =>
                item.Tag is DisplayInfo display && display.Id == selectedId)
            ?? DisplaySelector.Items.OfType<ComboBoxItem>().FirstOrDefault();

        UpdateDisplayStatus();
    }

    private async Task InitializeMediaAsync()
    {
        await _mediaService.InitializeAsync();
    }

    private void UpdateSurfaceStatus()
    {
        if (_probeBar is null)
        {
            SurfaceStatusText.Text = "Probe bar not created";
            DockingStatusText.Text = "Placement: Floating; AppBar: Unregistered";
            return;
        }

        var nativeWindow = _probeBar.NativeWindow;
        var currentDisplay = _displayService.GetForWindow(nativeWindow.WindowId);
        SurfaceStatusText.Text =
            $"HWND: 0x{_probeBar.Hwnd:X} | " +
            $"Position: {nativeWindow.AppWindow.Position.X}, {nativeWindow.AppWindow.Position.Y} | " +
            $"Size: {nativeWindow.AppWindow.Size.Width} × {nativeWindow.AppWindow.Size.Height} px | " +
            $"Display: {currentDisplay.Name} | " +
            $"DPI: {WindowPositioner.GetDpi(_probeBar.Hwnd)} | " +
            $"Backdrop: {_probeBar.BackdropStatus}";
        DockingStatusText.Text =
            $"Placement: {_probeBar.Placement.Mode}" +
            (_probeBar.Placement.Edge is null ? string.Empty : $" / {_probeBar.Placement.Edge}") +
            $" | AppBar: {(_probeBar.IsAppBarRegistered ? "Registered" : "Unregistered")}";
    }

    private void UpdateDisplayStatus()
    {
        var display = SelectedDisplay;
        DisplayStatusText.Text =
            $"Primary: {display.IsPrimary} | " +
            $"Bounds: {Format(display.Bounds)} | " +
            $"Work area: {Format(display.WorkArea)}";
    }

    private void UpdateMediaStatus(MediaSessionSnapshot snapshot)
    {
        MediaStatusText.Text =
            $"Source: {ValueOrNone(snapshot.SourceApplication)} | " +
            $"Title: {ValueOrNone(snapshot.Title)} | " +
            $"Artist: {ValueOrNone(snapshot.Artist)} | " +
            $"Playback: {snapshot.PlaybackState} | Status: {snapshot.Status}";
        _probeBar?.UpdateMedia(snapshot);
    }

    private static string Format(Windows.Graphics.RectInt32 rectangle) =>
        $"{rectangle.X}, {rectangle.Y}, {rectangle.Width} × {rectangle.Height} px";

    private static string ValueOrNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value;

    private void OnDisplaysChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(PopulateDisplays);

    private void OnMediaStateChanged(MediaSessionSnapshot snapshot) =>
        DispatcherQueue.TryEnqueue(() => UpdateMediaStatus(snapshot));

    private void OnProbeStateChanged(object? sender, EventArgs args) =>
        UpdateSurfaceStatus();

    private void OnProbeClosed(object sender, WindowEventArgs args)
    {
        if (_probeBar is null)
        {
            return;
        }

        _probeBar.StateChanged -= OnProbeStateChanged;
        _probeBar.Closed -= OnProbeClosed;
        _probeBar.Dispose();
        _probeBar = null;
        UpdateSurfaceStatus();
    }

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();

    private void ShowProbeBar_Click(object sender, RoutedEventArgs args)
    {
        EnsureProbeBar().ShowSurface();
        UpdateSurfaceStatus();
    }

    private void HideProbeBar_Click(object sender, RoutedEventArgs args)
    {
        _probeBar?.HideSurface();
        UpdateSurfaceStatus();
    }

    private void CenterProbeBar_Click(object sender, RoutedEventArgs args)
    {
        EnsureProbeBar().CenterOn(SelectedDisplay);
        UpdateSurfaceStatus();
    }

    private void RefreshDisplays_Click(object sender, RoutedEventArgs args)
    {
        _displayService.Refresh();
        PopulateDisplays();
    }

    private void DisplaySelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs args)
    {
        if (DisplayStatusText is not null && DisplaySelector.SelectedItem is not null)
        {
            UpdateDisplayStatus();
        }
    }

    private void DockProbeBar_Click(object sender, RoutedEventArgs args)
    {
        var edge = DockEdgeSelector.SelectedItem is ComboBoxItem { Tag: string value } &&
            Enum.TryParse<DockEdge>(value, out var parsed)
                ? parsed
                : DockEdge.Top;

        try
        {
            EnsureProbeBar().Dock(SelectedDisplay, edge);
        }
        catch (Exception exception)
        {
            DockingStatusText.Text = $"Dock failed; safe floating state requested: {exception.Message}";
            _probeBar?.Undock(SelectedDisplay);
        }

        UpdateSurfaceStatus();
    }

    private void UndockProbeBar_Click(object sender, RoutedEventArgs args)
    {
        EnsureProbeBar().Undock(SelectedDisplay);
        UpdateSurfaceStatus();
    }

    private void TriggerAnimation_Click(object sender, RoutedEventArgs args) =>
        EnsureProbeBar().TriggerAnimation();

    private async void Previous_Click(object sender, RoutedEventArgs args) =>
        _ = await _mediaService.TrySkipPreviousAsync();

    private async void PlayPause_Click(object sender, RoutedEventArgs args) =>
        _ = await _mediaService.TryTogglePlayPauseAsync();

    private async void Next_Click(object sender, RoutedEventArgs args) =>
        _ = await _mediaService.TrySkipNextAsync();
}
