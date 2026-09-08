using Xunit;

namespace Glass.Widgets.BuiltIn.Tests;

public sealed class PrivacyContractTests
{
    [Fact]
    public void ClipboardImplementationDoesNotReferencePersistenceOrDiagnostics()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root,
            "src", "Glass.Platform.Windows", "Clipboard", "ClipboardService.cs"));
        Assert.DoesNotContain("ILocalStateStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnostic", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Write", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NotesAreDocumentedAsPerInstanceLocalStateAndNeverDiagnostics()
    {
        var root = FindRepositoryRoot();
        var privacy = File.ReadAllText(Path.Combine(root, "docs", "PRIVACY_SECURITY.md"));
        Assert.Contains("Quick Notes", privacy, StringComparison.Ordinal);
        Assert.Contains("never written to diagnostics", privacy, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Glass.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
