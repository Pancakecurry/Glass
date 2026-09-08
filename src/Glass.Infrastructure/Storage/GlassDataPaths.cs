namespace Glass.Infrastructure.Storage;

public sealed class GlassDataPaths
{
    public GlassDataPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        StateDirectory = Path.Combine(RootDirectory, "state");
        BackupDirectory = Path.Combine(RootDirectory, "backups");
        LogDirectory = Path.Combine(RootDirectory, "logs");
    }

    public string RootDirectory { get; }

    public string StateDirectory { get; }

    public string BackupDirectory { get; }

    public string LogDirectory { get; }

    public static GlassDataPaths CreateDefault() =>
        new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Glass"));

    public void EnsureCreated()
    {
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
