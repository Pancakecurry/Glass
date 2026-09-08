using Glass.Widgets.Abstractions;
using Glass.Widgets.Runtime;

namespace Glass.Widgets.BuiltIn;

public sealed record QuickNotesState(string Text);

public sealed class QuickNotesModel : IAsyncDisposable
{
    private readonly WidgetInstanceId _instanceId;
    private readonly WidgetStateStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private CancellationTokenSource? _pendingSave;
    private Task _saveTask = Task.CompletedTask;

    public QuickNotesModel(WidgetInstanceId instanceId, WidgetStateStore store,
        TimeProvider? timeProvider = null)
    {
        _instanceId = instanceId;
        _store = store;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Text { get; private set; } = string.Empty;

    public async ValueTask LoadAsync(CancellationToken token = default) =>
        Text = (await _store.LoadAsync<QuickNotesState>(_instanceId, token)
            .ConfigureAwait(false))?.Text ?? string.Empty;

    public void Update(string text)
    {
        Text = text ?? string.Empty;
        lock (_gate)
        {
            _pendingSave?.Cancel();
            _pendingSave?.Dispose();
            _pendingSave = new CancellationTokenSource();
            _saveTask = SaveAfterDelayAsync(_pendingSave.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task pending;
        lock (_gate)
        {
            _pendingSave?.Cancel();
            pending = _saveTask;
        }
        try { await pending.ConfigureAwait(false); } catch (OperationCanceledException) { }
        await _store.SaveAsync(_instanceId, new QuickNotesState(Text)).ConfigureAwait(false);
        _pendingSave?.Dispose();
    }

    private async Task SaveAfterDelayAsync(CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), _timeProvider, token).ConfigureAwait(false);
        await _store.SaveAsync(_instanceId, new QuickNotesState(Text), token).ConfigureAwait(false);
    }
}
