using System.Text;
using Glass.Core.Persistence;

namespace Glass.Infrastructure.Storage;

public sealed class AtomicJsonStateStore : ILocalStateStore
{
    private readonly GlassDataPaths _paths;

    public AtomicJsonStateStore(GlassDataPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _paths.EnsureCreated();
    }

    public async ValueTask<string?> ReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var path = GetStatePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(
        string key,
        string serializedState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializedState);
        var path = GetStatePath(key);
        var temporaryPath = Path.Combine(
            _paths.StateDirectory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                    temporaryPath,
                    serializedState,
                    new UTF8Encoding(false),
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public ValueTask DeleteAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetStatePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return ValueTask.CompletedTask;
    }

    public string GetStatePath(string key)
    {
        ValidateKey(key);
        return Path.Combine(_paths.StateDirectory, $"{key}.json");
    }

    public string Backup(string key, string reason)
    {
        var source = GetStatePath(key);
        if (!File.Exists(source))
        {
            return string.Empty;
        }

        var safeReason = new string(reason
            .Where(character => char.IsLetterOrDigit(character) || character == '-')
            .ToArray());
        var destination = Path.Combine(
            _paths.BackupDirectory,
            $"{key}.{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}.{safeReason}.json");
        File.Move(source, destination);
        return destination;
    }

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Length > 64 || key.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException(
                "State keys may contain only ASCII letters, digits, hyphens, and underscores.",
                nameof(key));
        }
    }
}
