namespace Glass.Core.Persistence;

/// <summary>
/// Minimal boundary for locally persisted serialized state.
/// Serialization and storage location choices belong to an implementation layer.
/// </summary>
public interface ILocalStateStore
{
    ValueTask<string?> ReadAsync(
        string key,
        CancellationToken cancellationToken = default);

    ValueTask WriteAsync(
        string key,
        string serializedState,
        CancellationToken cancellationToken = default);
}
