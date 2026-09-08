using System.Runtime.InteropServices;
using Glass.Platform.Windows.Windowing;
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

public sealed partial class ClipboardService : IDisposable
{
    private const uint WmClipboardUpdate = 0x031D;
    private readonly nint _hwnd;
    private readonly IDisposable _messageRegistration;
    private bool _disposed;

    public ClipboardService(nint hwnd, NativeWindowMessageRouter messages)
    {
        _hwnd = hwnd;
        if (!AddClipboardFormatListener(hwnd))
            throw new InvalidOperationException("AddClipboardFormatListener failed.");
        _messageRegistration = messages.Register(WmClipboardUpdate, OnClipboardChanged);
    }

    public event EventHandler? Changed;

    public async ValueTask<string?> TryReadTextAsync()
    {
        var content = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        return content.Contains(StandardDataFormats.Text) ? await content.GetTextAsync() : null;
    }

    public async ValueTask<ClipboardHistorySnapshot> TryReadHistoryAsync()
    {
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
                catch { }
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
        _messageRegistration.Dispose();
        _ = RemoveClipboardFormatListener(_hwnd);
        Changed = null;
    }

    private bool OnClipboardChanged(nuint wParam, nint lParam, out nint result)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        result = 0;
        return true;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool AddClipboardFormatListener(nint hwnd);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static partial bool RemoveClipboardFormatListener(nint hwnd);
}
