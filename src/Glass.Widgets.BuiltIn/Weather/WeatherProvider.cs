using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Glass.Core.Persistence;
using Glass.Core.Product;

namespace Glass.Widgets.BuiltIn.Weather;

public sealed record WeatherLocation(double Latitude, double Longitude, string Label);
public sealed record WeatherSnapshot(
    WeatherLocation Location,
    DateTimeOffset ObservedAt,
    double AirTemperatureCelsius,
    double? WindSpeedMetersPerSecond,
    string SymbolCode,
    bool IsStale,
    string Attribution = "Weather data: MET Norway (CC BY 4.0)")
{
    public double? HighTemperatureCelsius { get; init; }
    public double? LowTemperatureCelsius { get; init; }
    public IReadOnlyList<WeatherForecastPoint> Forecast { get; init; } = [];
}

public sealed record WeatherForecastPoint(
    DateTimeOffset At,
    double AirTemperatureCelsius,
    string SymbolCode);

public interface IWeatherProvider
{
    ValueTask<WeatherSnapshot?> GetAsync(WeatherLocation location,
        CancellationToken cancellationToken = default);
    ValueTask ClearCacheAsync(CancellationToken cancellationToken = default);
}

public sealed class MetNorwayWeatherProvider : IWeatherProvider, IDisposable
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
        if (_client.DefaultRequestHeaders.UserAgent.Count == 0)
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"Glass/{ProductVersion.Current.Informational} (+https://github.com/Pancakecurry/Glass)");
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

    public void Dispose() => _gate.Dispose();

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
        var timeseries = json.RootElement.GetProperty("properties").GetProperty("timeseries");
        var instant = timeseries[0];
        var details = instant.GetProperty("data").GetProperty("instant").GetProperty("details");
        var symbol = string.Empty;
        if (instant.GetProperty("data").TryGetProperty("next_1_hours", out var next))
            symbol = next.GetProperty("summary").GetProperty("symbol_code").GetString() ?? string.Empty;
        var observedAt = instant.GetProperty("time").GetDateTimeOffset();
        var temperatures = timeseries.EnumerateArray()
            .Where(item => item.GetProperty("time").GetDateTimeOffset() <= observedAt.AddHours(24))
            .Select(ReadTemperature)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var forecast = timeseries.EnumerateArray()
            .Skip(1)
            .Where((_, index) => index % 3 == 0)
            .Take(4)
            .Select(item => new WeatherForecastPoint(
                item.GetProperty("time").GetDateTimeOffset(),
                ReadTemperature(item) ?? details.GetProperty("air_temperature").GetDouble(),
                ReadSymbol(item)))
            .ToArray();
        return new WeatherSnapshot(location,
            observedAt,
            details.GetProperty("air_temperature").GetDouble(),
            details.TryGetProperty("wind_speed", out var wind) ? wind.GetDouble() : null,
            symbol, false)
        {
            HighTemperatureCelsius = temperatures.Length == 0 ? null : temperatures.Max(),
            LowTemperatureCelsius = temperatures.Length == 0 ? null : temperatures.Min(),
            Forecast = forecast,
        };
    }

    private static double? ReadTemperature(JsonElement item)
    {
        var details = item.GetProperty("data").GetProperty("instant").GetProperty("details");
        return details.TryGetProperty("air_temperature", out var temperature)
            ? temperature.GetDouble() : null;
    }

    private static string ReadSymbol(JsonElement item)
    {
        var data = item.GetProperty("data");
        foreach (var period in new[] { "next_1_hours", "next_6_hours", "next_12_hours" })
            if (data.TryGetProperty(period, out var next) &&
                next.TryGetProperty("summary", out var summary) &&
                summary.TryGetProperty("symbol_code", out var symbol))
                return symbol.GetString() ?? string.Empty;
        return string.Empty;
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
