using Windows.Storage.Pickers;
using Windows.Storage;

namespace Glass.Platform.Windows.Pickers;

public sealed class OwnedPickerService(nint ownerWindow)
{
    public async ValueTask<string?> PickFileAsync(
        IEnumerable<string>? extensions = null,
        CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker();
        global::WinRT.Interop.InitializeWithWindow.Initialize(picker, ownerWindow);
        picker.FileTypeFilter.Clear();
        foreach (var extension in extensions ?? ["*"])
            picker.FileTypeFilter.Add(extension);
        var file = await picker.PickSingleFileAsync().AsTask(cancellationToken);
        return file?.Path;
    }

    public async ValueTask<string?> PickFolderAsync(
        CancellationToken cancellationToken = default)
    {
        var picker = new FolderPicker();
        global::WinRT.Interop.InitializeWithWindow.Initialize(picker, ownerWindow);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync().AsTask(cancellationToken);
        return folder?.Path;
    }

    public async ValueTask<bool> SaveTextAsync(
        string suggestedFileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentNullException.ThrowIfNull(content);
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
        };
        global::WinRT.Interop.InitializeWithWindow.Initialize(picker, ownerWindow);
        picker.FileTypeChoices.Add("Text document", [".txt"]);
        var file = await picker.PickSaveFileAsync().AsTask(cancellationToken);
        if (file is null) return false;
        await FileIO.WriteTextAsync(file, content).AsTask(cancellationToken);
        return true;
    }
}
