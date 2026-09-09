using Glass.Infrastructure.Storage;

namespace Glass.Infrastructure.Recovery;

public sealed record ConfigurationResetResult(string BackupDirectory, int RemovedFiles);

public sealed class ConfigurationRecoveryService(GlassDataPaths paths)
{
    private static readonly string[] ConfigurationFiles =
        ["settings.json", "shell-layout.json"];

    public ConfigurationResetResult ResetConfiguration(bool includeWidgetContents = false)
    {
        paths.EnsureCreated();
        var backup = Path.Combine(paths.BackupDirectory,
            $"reset-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}");
        Directory.CreateDirectory(backup);

        var candidates = Directory.EnumerateFiles(paths.StateDirectory, "*.json")
            .Where(path => includeWidgetContents ||
                ConfigurationFiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        foreach (var source in candidates)
            File.Move(source, Path.Combine(backup, Path.GetFileName(source)), false);
        return new ConfigurationResetResult(backup, candidates.Length);
    }
}
