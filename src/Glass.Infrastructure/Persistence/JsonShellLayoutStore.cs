using System.Text.Json;
using System.Text.Json.Serialization;
using Glass.Core.Persistence;
using Glass.Core.Shell;
using Glass.Infrastructure.Diagnostics;
using Glass.Infrastructure.Storage;

namespace Glass.Infrastructure.Persistence;

public sealed class JsonShellLayoutStore : IShellLayoutStore
{
    public const int CurrentSchemaVersion = 1;
    private const string StateKey = "shell-layout";
    private readonly AtomicJsonStateStore _stateStore;
    private readonly LocalDiagnosticLog? _diagnostics;
    private readonly JsonSerializerOptions _options;

    public JsonShellLayoutStore(
        AtomicJsonStateStore stateStore,
        LocalDiagnosticLog? diagnostics = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _diagnostics = diagnostics;
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public async ValueTask<ShellLayout> LoadAsync(
        ShellLayout fallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        string? serialized;
        try
        {
            serialized = await _stateStore.ReadAsync(StateKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await TryWriteDiagnosticAsync(
                    $"Could not read shell layout; defaults retained; error={exception.Message}",
                    cancellationToken)
                .ConfigureAwait(false);
            return fallback;
        }
        if (serialized is null)
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(serialized);
            var root = document.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var schemaElement) ||
                !schemaElement.TryGetInt32(out var schemaVersion))
            {
                return await RecoverAsync("missing-schema", fallback, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (schemaVersion > CurrentSchemaVersion)
            {
                return await RecoverAsync("future-schema", fallback, cancellationToken)
                    .ConfigureAwait(false);
            }

            var payload = Migrate(schemaVersion, root);
            return payload?.Normalize() ??
                await RecoverAsync("invalid-payload", fallback, cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            return await RecoverAsync(
                    $"malformed-json-{exception.LineNumber}",
                    fallback,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask SaveAsync(
        ShellLayout layout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var document = new VersionedDocument<ShellLayout>(
            CurrentSchemaVersion,
            layout.Normalize());
        var serialized = JsonSerializer.Serialize(document, _options);
        await _stateStore.WriteAsync(StateKey, serialized, cancellationToken)
            .ConfigureAwait(false);
    }

    private ShellLayout? Migrate(int schemaVersion, JsonElement root)
    {
        // Schema 0 was reserved during development and used the same payload shape.
        if (schemaVersion is not (0 or CurrentSchemaVersion) ||
            !root.TryGetProperty("payload", out var payload))
        {
            return null;
        }

        return payload.Deserialize<ShellLayout>(_options);
    }

    private async ValueTask<ShellLayout> RecoverAsync(
        string reason,
        ShellLayout fallback,
        CancellationToken cancellationToken)
    {
        var backup = "unavailable";
        try
        {
            backup = _stateStore.Backup(StateKey, reason);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            backup = $"failed: {exception.Message}";
        }

        await TryWriteDiagnosticAsync(
                $"Recovered shell layout with defaults; reason={reason}; backup={backup}",
                cancellationToken)
            .ConfigureAwait(false);

        return fallback;
    }

    private async ValueTask TryWriteDiagnosticAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (_diagnostics is null)
        {
            return;
        }

        try
        {
            await _diagnostics.WriteAsync("persistence", message, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Local persistence diagnostic failed: {exception}");
        }
    }

    private sealed record VersionedDocument<T>(int SchemaVersion, T Payload);
}
