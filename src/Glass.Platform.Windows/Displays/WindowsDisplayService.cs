using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Glass.Platform.Windows.Displays;

public sealed class WindowsDisplayService : IDisposable
{
    private readonly DisplayAreaWatcher _watcher;
    private IReadOnlyList<DisplayInfo> _displays = Array.Empty<DisplayInfo>();
    private bool _disposed;

    public WindowsDisplayService()
    {
        _watcher = DisplayArea.CreateWatcher();
        _watcher.Added += OnDisplayChanged;
        _watcher.Removed += OnDisplayChanged;
        _watcher.Updated += OnDisplayChanged;
        Refresh();
        _watcher.Start();
    }

    public event EventHandler? DisplaysChanged;

    public IReadOnlyList<DisplayInfo> Displays => _displays;

    public DisplayInfo PrimaryDisplay
    {
        get
        {
            var primary = _displays.FirstOrDefault(display => display.IsPrimary);
            if (primary is not null)
            {
                return primary;
            }

            return _displays.Count > 0
                ? _displays[0]
                : throw new InvalidOperationException("Windows reported no display areas.");
        }
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _displays = DisplayArea.FindAll()
            .Select(CreateDisplayInfo)
            .OrderByDescending(display => display.IsPrimary)
            .ThenBy(display => display.Bounds.X)
            .ThenBy(display => display.Bounds.Y)
            .ToArray();
    }

    public DisplayInfo GetForWindow(WindowId windowId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        return area is null ? PrimaryDisplay : CreateDisplayInfo(area);
    }

    public DisplayInfo? Find(DisplayId displayId) =>
        _displays.FirstOrDefault(display => display.Id == displayId);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Added -= OnDisplayChanged;
        _watcher.Removed -= OnDisplayChanged;
        _watcher.Updated -= OnDisplayChanged;
        _watcher.Stop();
        DisplaysChanged = null;
    }

    private static DisplayInfo CreateDisplayInfo(DisplayArea area)
    {
        var bounds = area.OuterBounds;
        var relativeWorkArea = area.WorkArea;
        var workArea = new RectInt32(
            bounds.X + relativeWorkArea.X,
            bounds.Y + relativeWorkArea.Y,
            relativeWorkArea.Width,
            relativeWorkArea.Height);

        return new DisplayInfo(
            area.DisplayId,
            area.IsPrimary ? "Primary display" : $"Display {area.DisplayId}",
            area.IsPrimary,
            bounds,
            workArea);
    }

    private void OnDisplayChanged(DisplayAreaWatcher sender, DisplayArea args)
    {
        if (_disposed)
        {
            return;
        }

        Refresh();
        DisplaysChanged?.Invoke(this, EventArgs.Empty);
    }
}
