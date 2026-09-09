using System.Text.Json;
using Glass.Core.Persistence;
using Glass.Core.Runtime;

namespace Glass.Infrastructure.Recovery;

public sealed class SessionHealthStore(ILocalStateStore store, TimeProvider? timeProvider = null)
{
    private const string StateKey = "session-health";
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async ValueTask<SessionStartDecision> BeginAsync(
        bool explicitSafeMode,
        CancellationToken cancellationToken = default)
    {
        SessionHealthState? state = null;
        var serialized = await store.ReadAsync(StateKey, cancellationToken).ConfigureAwait(false);
        if (serialized is not null)
        {
            try { state = JsonSerializer.Deserialize<SessionHealthState>(serialized, _json); }
            catch (JsonException) { state = null; }
        }
        var decision = SessionHealthRules.Begin(
            state, _timeProvider.GetUtcNow(), explicitSafeMode);
        await SaveAsync(decision.NextState, cancellationToken).ConfigureAwait(false);
        return decision;
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        var serialized = await store.ReadAsync(StateKey, cancellationToken).ConfigureAwait(false);
        SessionHealthState state;
        try
        {
            state = serialized is null
                ? new SessionHealthState()
                : JsonSerializer.Deserialize<SessionHealthState>(serialized, _json) ?? new();
        }
        catch (JsonException) { state = new SessionHealthState(); }
        await SaveAsync(SessionHealthRules.Complete(state, _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SessionHealthState> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var serialized = await store.ReadAsync(StateKey, cancellationToken).ConfigureAwait(false);
        if (serialized is null) return new();
        try { return JsonSerializer.Deserialize<SessionHealthState>(serialized, _json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private ValueTask SaveAsync(SessionHealthState state, CancellationToken token) =>
        store.WriteAsync(StateKey, JsonSerializer.Serialize(state, _json), token);
}
