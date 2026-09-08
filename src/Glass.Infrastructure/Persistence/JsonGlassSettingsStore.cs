using System.Text.Json;
using System.Text.Json.Serialization;
using Glass.Core.Appearance;
using Glass.Core.Persistence;
using Glass.Infrastructure.Diagnostics;
using Glass.Infrastructure.Storage;

namespace Glass.Infrastructure.Persistence;

public sealed class JsonGlassSettingsStore : IGlassSettingsStore
{
    public const int CurrentSchemaVersion = 1;
    private const string StateKey = "settings";
    private readonly AtomicJsonStateStore _stateStore;
    private readonly LocalDiagnosticLog? _diagnostics;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public JsonGlassSettingsStore(
        AtomicJsonStateStore stateStore,
        LocalDiagnosticLog? diagnostics = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _diagnostics = diagnostics;
    }

    public async ValueTask<GlassSettings> LoadAsync(
        GlassSettings fallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        try
        {
            var json = await _stateStore.ReadAsync(StateKey, cancellationToken)
                .ConfigureAwait(false);
            if (json is null) return fallback.Normalize();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var versionElement) ||
                !versionElement.TryGetInt32(out var version) ||
                version is < 0 or > CurrentSchemaVersion ||
                !root.TryGetProperty("payload", out var payload))
            {
                return await RecoverAsync("unsupported-schema", fallback, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Schema 0 contained the same global fields and no override collections.
            return (payload.Deserialize<GlassSettings>(_options) ?? fallback).Normalize();
        }
        catch (JsonException exception)
        {
            return await RecoverAsync(
                $"malformed-json-{exception.LineNumber}", fallback, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteDiagnosticAsync(
                $"Could not load settings; defaults retained; error={exception.Message}",
                cancellationToken).ConfigureAwait(false);
            return fallback.Normalize();
        }
    }

    public ValueTask SaveAsync(
        GlassSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var json = JsonSerializer.Serialize(
            new VersionedDocument<GlassSettings>(CurrentSchemaVersion, settings.Normalize()),
            _options);
        return _stateStore.WriteAsync(StateKey, json, cancellationToken);
    }

    private async ValueTask<GlassSettings> RecoverAsync(
        string reason,
        GlassSettings fallback,
        CancellationToken cancellationToken)
    {
        var backup = "unavailable";
        try { backup = _stateStore.Backup(StateKey, reason); }
        catch (Exception exception) { backup = $"failed: {exception.Message}"; }
        await WriteDiagnosticAsync(
            $"Recovered settings with defaults; reason={reason}; backup={backup}",
            cancellationToken).ConfigureAwait(false);
        return fallback.Normalize();
    }

    private async ValueTask WriteDiagnosticAsync(string message, CancellationToken token)
    {
        if (_diagnostics is null) return;
        try { await _diagnostics.WriteAsync("settings", message, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Settings diagnostic failed: {exception}");
        }
    }

    private sealed record VersionedDocument<T>(int SchemaVersion, T Payload);
}
