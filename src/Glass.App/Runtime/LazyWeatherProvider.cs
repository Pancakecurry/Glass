using Glass.Core.Persistence;
using Glass.Core.Product;
using Glass.Widgets.BuiltIn.Weather;

namespace Glass.App.Runtime;

internal sealed class LazyWeatherProvider(ILocalStateStore store) : IWeatherProvider, IDisposable
{
    private readonly object _gate = new();
    private HttpClient? _client;
    private MetNorwayWeatherProvider? _provider;
    private bool _disposed;

    public ValueTask<WeatherSnapshot?> GetAsync(
        WeatherLocation location,
        CancellationToken cancellationToken = default) =>
        GetProvider().GetAsync(location, cancellationToken);

    public ValueTask ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_provider is null) return store.DeleteAsync("weather-cache", cancellationToken);
            return _provider.ClearCacheAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _provider?.Dispose();
            _client?.Dispose();
            _provider = null;
            _client = null;
        }
    }

    private MetNorwayWeatherProvider GetProvider()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_provider is not null) return _provider;
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"Glass/{ProductVersion.Current.Informational} (+https://github.com/Pancakecurry/Glass)");
            return _provider = new MetNorwayWeatherProvider(_client, store);
        }
    }
}
