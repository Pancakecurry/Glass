using System.Text;

namespace Glass.Infrastructure.Diagnostics;

public sealed class LocalDiagnosticLog
{
    private const long MaximumBytes = 512 * 1024;
    private readonly string _path;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public LocalDiagnosticLog(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        Directory.CreateDirectory(logDirectory);
        _path = Path.Combine(logDirectory, "glass.log");
    }

    public async ValueTask WriteAsync(
        string category,
        string message,
        CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length >= MaximumBytes)
            {
                File.Move(_path, $"{_path}.previous", true);
            }

            var line = $"{DateTimeOffset.UtcNow:O} [{category}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(
                    _path,
                    line,
                    new UTF8Encoding(false),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
