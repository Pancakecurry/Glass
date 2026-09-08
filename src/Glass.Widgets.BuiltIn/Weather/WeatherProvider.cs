using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Glass.Core.Persistence;

namespace Glass.Widgets.BuiltIn.Weather;

public sealed record WeatherLocation(double Latitude, double Longitude, string Label);
public sealed record WeatherSnapshot(
    WeatherLocation Location,
    DateTimeOffset ObservedAt,
    double AirTemperatureCelsius,
    double? WindSpeedMetersPerSecond,
    string SymbolCode,
    bool IsStale,
    string Attribution = "Weather data: MET Norway (CC BY 4.0)");

public interface IWeatherProvider
{
    ValueTask<WeatherSnapshot?> GetAsync(WeatherLocation location,
        CancellationToken cancellationToken = default);
    ValueTask ClearCacheAsync(CancellationToken cancellationToken = default);
}

public sealed class MetNorwayWeatherProvider : IWeatherProvider
{
    private const string CacheKey = "weather-cache";
    private readonly HttpClient _client;
    private readonly ILocalStateStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public MetNorwayWeatherProvider(HttpClient client, ILocalStateStore store,
        TimeProvider? timeProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Glass/0.2 (+https://github.com/Pancakecurry/Glass)");
    }

    public async ValueTask<WeatherSnapshot?> GetAsync(
        WeatherLocation location,
        CancellationToken cancellationToken = default)
    {
        Validate(location);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cached = await ReadCacheAsync(cancellationToken).ConfigureAwait(false);
            var matches = cached is not null && cached.Location == location;
            if (matches && cached!.ExpiresAt > _timeProvider.GetUtcNow())
                return cached.Snapshot with { IsStale = false };

            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={location.Latitude.ToString("0.####", CultureInfo.InvariantCulture)}&lon={location.Longitude.ToString("0.####", CultureInfo.InvariantCulture)}");
            if (matches && cached!.LastModified is { } modified)
                request.Headers.IfModifiedSince = modified;
            try
            {
                using var response = await _client.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotModified && matches)
                {
                    var refreshed = cached! with { ExpiresAt = Expiry(response.Headers) };
                    await WriteCacheAsync(refreshed, cancellationToken).ConfigureAwait(false);
                    return refreshed.Snapshot with { IsStale = false };
                }
                response.EnsureSuccessStatusCode();
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                var snapshot = await ParseAsync(body, location, cancellationToken).ConfigureAwait(false);
                var document = new CacheDocument(location, snapshot,
                    response.Content.Headers.LastModified,
                    Expiry(response.Headers));
                await WriteCacheAsync(document, cancellationToken).ConfigureAwait(false);
                return snapshot;
            }
            catch (Exception exception) when (
                (exception is HttpRequestException || exception is TaskCanceledException) &&
                !cancellationToken.IsCancellationRequested)
            {
                return matches ? cached!.Snapshot with { IsStale = true } : null;
            }
        }
        finally { _gate.Release(); }
    }

    public ValueTask ClearCacheAsync(CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(CacheKey, cancellationToken);

    private async ValueTask<CacheDocument?> ReadCacheAsync(CancellationToken token)
    {
        var json = await _store.ReadAsync(CacheKey, token).ConfigureAwait(false);
        return json is null ? null : JsonSerializer.Deserialize<CacheDocument>(json, _json);
    }
    private ValueTask WriteCacheAsync(CacheDocument document, CancellationToken token) =>
        _store.WriteAsync(CacheKey, JsonSerializer.Serialize(document, _json), token);

    private DateTimeOffset Expiry(HttpResponseHeaders headers) =>
        headers.CacheControl?.MaxAge is { } maxAge
            ? _timeProvider.GetUtcNow() + maxAge
            : _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(30);

    private static async ValueTask<WeatherSnapshot> ParseAsync(Stream body,
        WeatherLocation location, CancellationToken token)
    {
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: token)
            .ConfigureAwait(false);
        var instant = json.RootElement.GetProperty("properties").GetProperty("timeseries")[0];
        var details = instant.GetProperty("data").GetProperty("instant").GetProperty("details");
        var symbol = string.Empty;
        if (instant.GetProperty("data").TryGetProperty("next_1_hours", out var next))
            symbol = next.GetProperty("summary").GetProperty("symbol_code").GetString() ?? string.Empty;
        return new WeatherSnapshot(location,
            instant.GetProperty("time").GetDateTimeOffset(),
            details.GetProperty("air_temperature").GetDouble(),
            details.TryGetProperty("wind_speed", out var wind) ? wind.GetDouble() : null,
            symbol, false);
    }

    private static void Validate(WeatherLocation location)
    {
        if (!double.IsFinite(location.Latitude) || location.Latitude is < -90 or > 90 ||
            !double.IsFinite(location.Longitude) || location.Longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(location));
    }
    private sealed record CacheDocument(WeatherLocation Location, WeatherSnapshot Snapshot,
        DateTimeOffset? LastModified, DateTimeOffset ExpiresAt);
}
