using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;
using Glass.Platform.Windows.Windowing;

namespace Glass.App.Runtime;

internal sealed class WidgetSurfaceManager : IDisposable
{
    private readonly WindowsDisplayService _displays;
    private readonly ProductSurfaceServices _services;
    private readonly Func<StandaloneWidgetDefinition, Task> _persist;
    private readonly Dictionary<Guid, WidgetWindow> _windows = [];
    private bool _disposed;

    public WidgetSurfaceManager(
        WindowsDisplayService displays,
        ProductSurfaceServices services,
        Func<StandaloneWidgetDefinition, Task> persist)
    {
        _displays = displays;
        _services = services;
        _persist = persist;
        _displays.DisplaysChanged += OnDisplaysChanged;
    }

    public void Sync(ShellLayout layout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var desired = layout.StandaloneWidgets.Where(surface => surface.IsEnabled)
            .ToDictionary(surface => surface.WidgetInstanceId);
        foreach (var id in _windows.Keys.Where(id => !desired.ContainsKey(id)).ToArray())
        {
            _windows[id].DefinitionSettled -= OnDefinitionSettled;
            _windows[id].Close();
            _windows[id].Dispose();
            _windows.Remove(id);
        }
        foreach (var surface in desired.Values)
        {
            var instance = layout.WidgetInstances.FirstOrDefault(widget =>
                widget.WidgetInstanceId == surface.WidgetInstanceId);
            if (instance is null) continue;
            if (_windows.TryGetValue(surface.WidgetInstanceId, out var existing))
            {
                existing.Apply(surface, instance);
                continue;
            }
            var window = new WidgetWindow(_displays, _services, surface, instance);
            window.DefinitionSettled += OnDefinitionSettled;
            _windows.Add(surface.WidgetInstanceId, window);
            window.Present();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _displays.DisplaysChanged -= OnDisplaysChanged;
        foreach (var window in _windows.Values)
        {
            window.DefinitionSettled -= OnDefinitionSettled;
            window.Close();
            window.Dispose();
        }
        _windows.Clear();
    }

    public void ApplyFullscreenSuppression(
        FullscreenWindowSnapshot snapshot,
        bool respectFullscreen)
    {
        foreach (var window in _windows.Values)
        {
            var target = window.Surface.Placement.Target.LastKnownBounds;
            var display = snapshot.DisplayBounds;
            var overlaps = Math.Max(target.X, display.X) <
                    Math.Min(target.X + target.Width, display.X + display.Width) &&
                Math.Max(target.Y, display.Y) <
                    Math.Min(target.Y + target.Height, display.Y + display.Height);
            window.SetFullscreenSuppressed(respectFullscreen && snapshot.IsFullscreen &&
                overlaps && window.Surface.ZOrder == Glass.Core.Shell.SurfaceZOrder.AlwaysOnTop);
        }
    }

    private async void OnDefinitionSettled(StandaloneWidgetDefinition definition)
    {
        try { await _persist(definition); }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Widget surface persistence failed: {exception}");
        }
    }

    private async void OnDisplaysChanged(object? sender, EventArgs args)
    {
        try
        {
            foreach (var window in _windows.Values)
            {
                var definition = window.ReconcileDisplay();
                await _persist(definition);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Widget display reconciliation failed: {exception}");
        }
    }
}
