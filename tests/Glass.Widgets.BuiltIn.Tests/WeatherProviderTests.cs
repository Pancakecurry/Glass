using System.Net;
using System.Text;
using Glass.Core.Persistence;
using Glass.Core.Product;
using Glass.Widgets.BuiltIn.Weather;
using Xunit;

namespace Glass.Widgets.BuiltIn.Tests;

public sealed class WeatherProviderTests
{
    [Fact]
    public async Task UsesFreshCacheWithoutDuplicateNetworkRequest()
    {
        var handler = new StubHandler();
        using var client = new HttpClient(handler);
        var store = new MemoryStore();
        using var provider = new MetNorwayWeatherProvider(client, store);
        var location = new WeatherLocation(59.91, 10.75, "Oslo");
        var token = TestContext.Current.CancellationToken;
        var first = await provider.GetAsync(location, token);
        var second = await provider.GetAsync(location, token);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Location, second.Location);
        Assert.Equal(first.ObservedAt, second.ObservedAt);
        Assert.Equal(first.AirTemperatureCelsius, second.AirTemperatureCelsius);
        Assert.Equal(first.Forecast.ToArray(), second.Forecast.ToArray());
        Assert.Equal(1, handler.Requests);
        Assert.Contains("MET Norway", first!.Attribution);
        Assert.Equal(4.5, first.HighTemperatureCelsius);
        Assert.Equal(4.5, first.LowTemperatureCelsius);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("api.met.no", request.RequestUri!.Host);
            Assert.Contains($"Glass/{ProductVersion.Current.Informational}",
                request.Headers.UserAgent.ToString());
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"properties":{"timeseries":[{"time":"2026-01-01T00:00:00Z","data":{"instant":{"details":{"air_temperature":4.5,"wind_speed":2.0}},"next_1_hours":{"summary":{"symbol_code":"clearsky_day"}}}}]}}
                    """, Encoding.UTF8, "application/json"),
            };
            response.Headers.CacheControl = new() { MaxAge = TimeSpan.FromHours(1) };
            return Task.FromResult(response);
        }
    }

    private sealed class MemoryStore : ILocalStateStore
    {
        private readonly Dictionary<string, string> _values = [];
        public ValueTask<string?> ReadAsync(string key, CancellationToken token = default) =>
            ValueTask.FromResult(_values.GetValueOrDefault(key));
        public ValueTask WriteAsync(string key, string value, CancellationToken token = default)
        { _values[key] = value; return ValueTask.CompletedTask; }
        public ValueTask DeleteAsync(string key, CancellationToken token = default)
        { _values.Remove(key); return ValueTask.CompletedTask; }
    }
}
