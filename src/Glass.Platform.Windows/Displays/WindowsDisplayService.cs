using Microsoft.UI;
using Microsoft.UI.Windowing;
using Glass.Core.Placement;
using Windows.Graphics;

namespace Glass.Platform.Windows.Displays;

public sealed class WindowsDisplayService : IDisposable
{
    private readonly DisplayAreaWatcher _watcher;
    private DisplayInfo[] _displays = [];
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

            return _displays.Length > 0
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

    public DisplayInfo Resolve(DisplayTarget target)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(target);

        var exact = _displays.FirstOrDefault(display =>
            string.Equals(display.PersistentId, target.PersistentId, StringComparison.Ordinal));
        if (exact is not null)
        {
            return exact;
        }

        var equivalent = _displays
            .Select(display => new
            {
                Display = display,
                Overlap = IntersectionArea(display.Bounds, target.LastKnownBounds),
            })
            .OrderByDescending(candidate => candidate.Overlap)
            .FirstOrDefault(candidate => candidate.Overlap > 0)?.Display;
        if (equivalent is not null)
        {
            return equivalent;
        }

        return target.WasPrimary || _displays.Length == 0 ? PrimaryDisplay : _displays[0];
    }

    public static DisplayTarget ToTarget(DisplayInfo display) =>
        new(
            display.PersistentId,
            display.IsPrimary,
            new NativePixelRect(
                display.Bounds.X,
                display.Bounds.Y,
                display.Bounds.Width,
                display.Bounds.Height));

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
            area.DisplayId.Value.ToString("X16", System.Globalization.CultureInfo.InvariantCulture),
            area.IsPrimary ? "Primary display" : $"Display {area.DisplayId}",
            area.IsPrimary,
            bounds,
            workArea);
    }

    private static long IntersectionArea(RectInt32 left, NativePixelRect right)
    {
        var width = Math.Max(0, Math.Min(left.X + left.Width, right.X + right.Width) -
            Math.Max(left.X, right.X));
        var height = Math.Max(0, Math.Min(left.Y + left.Height, right.Y + right.Height) -
            Math.Max(left.Y, right.Y));
        return (long)width * height;
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
