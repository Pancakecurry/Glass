using Glass.Core.Shell;
using Glass.Platform.Windows.Displays;

namespace Glass.App.Runtime;

internal sealed class WidgetSurfaceManager(
    WindowsDisplayService displays,
    ProductSurfaceServices services,
    Func<StandaloneWidgetDefinition, Task> persist) : IDisposable
{
    private readonly Dictionary<Guid, WidgetWindow> _windows = [];
    private bool _disposed;

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
            var window = new WidgetWindow(displays, services, surface, instance);
            window.DefinitionSettled += OnDefinitionSettled;
            _windows.Add(surface.WidgetInstanceId, window);
            window.Present();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var window in _windows.Values)
        {
            window.DefinitionSettled -= OnDefinitionSettled;
            window.Close();
            window.Dispose();
        }
        _windows.Clear();
    }

    private async void OnDefinitionSettled(StandaloneWidgetDefinition definition)
    {
        try { await persist(definition); }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Widget surface persistence failed: {exception}");
        }
    }
}
