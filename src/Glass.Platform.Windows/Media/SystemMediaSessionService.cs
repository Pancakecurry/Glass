using System.Diagnostics;
using Windows.Media.Control;

namespace Glass.Platform.Windows.Media;

public sealed class SystemMediaSessionService : IDisposable
{
    private readonly object _initializationGate = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private Task? _initializationTask;
    private bool _disposed;

    public MediaSessionSnapshot Current { get; private set; } =
        MediaSessionSnapshot.NoSession;

    public event Action<MediaSessionSnapshot>? StateChanged;

    public Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_initializationGate)
        {
            return _initializationTask ??= InitializeCoreAsync();
        }
    }

    public async Task<bool> TrySkipPreviousAsync() =>
        await InvokeControlAsync(session => session.TrySkipPreviousAsync());

    public async Task<bool> TryTogglePlayPauseAsync()
    {
        var session = _currentSession;
        if (session is null)
        {
            return false;
        }

        try
        {
            var playbackStatus = session.GetPlaybackInfo().PlaybackStatus;
            var succeeded = playbackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ? await session.TryPauseAsync()
                : await session.TryPlayAsync();
            return succeeded;
        }
        catch (Exception exception)
        {
            PublishError(exception);
            return false;
        }
    }

    public async Task<bool> TrySkipNextAsync() =>
        await InvokeControlAsync(session => session.TrySkipNextAsync());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachCurrentSession();

        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        _manager = null;
        StateChanged = null;
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            var manager =
                await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed)
            {
                return;
            }

            _manager = manager;
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            await ReplaceCurrentSessionAsync(manager.GetCurrentSession());
        }
        catch (Exception exception)
        {
            PublishError(exception);
        }
    }

    private async void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        try
        {
            await ReplaceCurrentSessionAsync(sender.GetCurrentSession());
        }
        catch (Exception exception)
        {
            PublishError(exception);
        }
    }

    private async void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        try
        {
            await RefreshAsync(sender);
        }
        catch (Exception exception)
        {
            PublishError(exception);
        }
    }

    private async void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        try
        {
            await RefreshAsync(sender);
        }
        catch (Exception exception)
        {
            PublishError(exception);
        }
    }

    private async Task ReplaceCurrentSessionAsync(
        GlobalSystemMediaTransportControlsSession? session)
    {
        if (_disposed)
        {
            return;
        }

        DetachCurrentSession();
        _currentSession = session;

        if (session is null)
        {
            Publish(MediaSessionSnapshot.NoSession);
            return;
        }

        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        await RefreshAsync(session);
    }

    private async Task RefreshAsync(
        GlobalSystemMediaTransportControlsSession session)
    {
        var properties = await session.TryGetMediaPropertiesAsync();
        if (_disposed || !ReferenceEquals(session, _currentSession))
        {
            return;
        }

        var playback = session.GetPlaybackInfo();
        Publish(new MediaSessionSnapshot(
            true,
            session.SourceAppUserModelId ?? string.Empty,
            properties?.Title ?? string.Empty,
            properties?.Artist ?? string.Empty,
            playback.PlaybackStatus.ToString(),
            "Active session"));
    }

    private async Task<bool> InvokeControlAsync(
        Func<GlobalSystemMediaTransportControlsSession, Windows.Foundation.IAsyncOperation<bool>>
            operation)
    {
        var session = _currentSession;
        if (session is null)
        {
            return false;
        }

        try
        {
            return await operation(session);
        }
        catch (Exception exception)
        {
            PublishError(exception);
            return false;
        }
    }

    private void DetachCurrentSession()
    {
        if (_currentSession is null)
        {
            return;
        }

        _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _currentSession = null;
    }

    private void Publish(MediaSessionSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        Current = snapshot;
        StateChanged?.Invoke(snapshot);
    }

    private void PublishError(Exception exception)
    {
        Debug.WriteLine($"System media session failure: {exception}");
        Publish(MediaSessionSnapshot.Unavailable(exception.Message));
    }
}
