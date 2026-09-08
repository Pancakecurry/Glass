using System.Text.Json;
using Glass.Core.Persistence;
using Glass.Widgets.Abstractions;

namespace Glass.Widgets.Runtime;

public sealed class WidgetStateStore(ILocalStateStore store)
{
    public const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async ValueTask<T?> LoadAsync<T>(WidgetInstanceId id, CancellationToken token = default)
    {
        var json = await store.ReadAsync(Key(id), token).ConfigureAwait(false);
        if (json is null) return default;
        var document = JsonSerializer.Deserialize<Document<T>>(json, Options);
        return document?.SchemaVersion == CurrentSchemaVersion ? document.Payload : default;
    }

    public ValueTask SaveAsync<T>(WidgetInstanceId id, T state,
        CancellationToken token = default)
    {
        var document = new Document<T>(CurrentSchemaVersion, state);
        return store.WriteAsync(Key(id), JsonSerializer.Serialize(document, Options), token);
    }

    public ValueTask DeleteAsync(WidgetInstanceId id, CancellationToken token = default) =>
        store.DeleteAsync(Key(id), token);

    private static string Key(WidgetInstanceId id) => $"widget-{id.Value:N}";
    private sealed record Document<T>(int SchemaVersion, T Payload);
}
