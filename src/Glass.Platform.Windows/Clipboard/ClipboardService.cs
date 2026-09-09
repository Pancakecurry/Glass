using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace Glass.Platform.Windows.Clipboard;

public enum ClipboardHistoryAvailability
{
    Available,
    Disabled,
    AccessDenied,
    Unavailable,
}

public sealed record ClipboardHistorySnapshot(
    ClipboardHistoryAvailability Availability,
    IReadOnlyList<string> TextItems);

public sealed class ClipboardService : IDisposable
{
    private bool _started;
    private bool _disposed;

    public event EventHandler? Changed;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_started) return ValueTask.CompletedTask;
        global::Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += OnClipboardChanged;
        _started = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stop();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<string?> TryReadTextAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var content = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        return content.Contains(StandardDataFormats.Text) ? await content.GetTextAsync() : null;
    }

    public async ValueTask<ClipboardHistorySnapshot> TryReadHistoryAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var result = await global::Windows.ApplicationModel.DataTransfer.Clipboard
                .GetHistoryItemsAsync();
            var availability = result.Status switch
            {
                ClipboardHistoryItemsResultStatus.Success =>
                    ClipboardHistoryAvailability.Available,
                ClipboardHistoryItemsResultStatus.ClipboardHistoryDisabled =>
                    ClipboardHistoryAvailability.Disabled,
                ClipboardHistoryItemsResultStatus.AccessDenied =>
                    ClipboardHistoryAvailability.AccessDenied,
                _ => ClipboardHistoryAvailability.Unavailable,
            };
            if (availability != ClipboardHistoryAvailability.Available)
                return new ClipboardHistorySnapshot(availability, []);

            var items = new List<string>();
            foreach (var historyItem in result.Items)
            {
                var content = historyItem.Content;
                if (!content.Contains(StandardDataFormats.Text)) continue;
                try
                {
                    var text = await content.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text)) items.Add(text);
                }
                catch (Exception exception) when (exception is COMException or
                    UnauthorizedAccessException)
                {
                    // History items can expire between enumeration and retrieval.
                    _ = exception;
                }
                if (items.Count >= 20) break;
            }
            return new ClipboardHistorySnapshot(availability, items);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            COMException or NotImplementedException)
        {
            return new ClipboardHistorySnapshot(
                exception is UnauthorizedAccessException
                    ? ClipboardHistoryAvailability.AccessDenied
                    : ClipboardHistoryAvailability.Unavailable,
                []);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        Changed = null;
    }

    private void Stop()
    {
        if (!_started) return;
        global::Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= OnClipboardChanged;
        _started = false;
    }

    private void OnClipboardChanged(object? sender, object args) =>
        Changed?.Invoke(this, EventArgs.Empty);
}
