using System.Runtime.InteropServices;
using Glass.Platform.Windows.Windowing;
using Windows.ApplicationModel.DataTransfer;

namespace Glass.Platform.Windows.Clipboard;

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
