using System.Net;
using System.Net.Http.Json;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Dashboard;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Dashboard;

public sealed class NwsWeatherProviderTests
{
    [Fact]
    public async Task ProviderRetriesOneTransientFailureAndMapsCurrentConditionsAndForecast()
    {
        var handler = new NwsHandler();
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new NwsWeatherProvider(
            new SingleClientFactory(client),
            cache,
            Options.Create(new WeatherConfiguration()));

        var result = await provider.GetAsync(40.7128m, -74.0060m, "celsius", CancellationToken.None);

        Assert.Equal(18m, result.Current!.Temperature);
        Assert.Equal("Sunny", result.Current.Summary);
        Assert.Equal("Today", Assert.Single(result.Forecast).Name);
        Assert.Equal("celsius", result.Forecast[0].TemperatureUnit);
        Assert.Equal(2, handler.PointsAttempts);
        Assert.False(result.IsStale);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class NwsHandler : HttpMessageHandler
    {
        public int PointsAttempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.StartsWith("/points/", StringComparison.Ordinal))
            {
                PointsAttempts++;
                if (PointsAttempts == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                return Json(new { properties = new { forecast = "https://api.weather.gov/gridpoints/OKX/1,1/forecast", observationStations = "https://api.weather.gov/gridpoints/OKX/1,1/stations" } });
            }
            if (path.EndsWith("/forecast", StringComparison.Ordinal))
                return Json(new { properties = new { periods = new[] { new { name = "Today", startTime = "2026-08-31T08:00:00-04:00", endTime = "2026-08-31T18:00:00-04:00", temperature = 22, temperatureUnit = "C", shortForecast = "Sunny", isDaytime = true } } } });
            if (path.EndsWith("/stations", StringComparison.Ordinal))
                return Json(new { features = new[] { new { id = "https://api.weather.gov/stations/KNYC" } } });
            if (path.EndsWith("/observations/latest", StringComparison.Ordinal))
                return Json(new { properties = new { temperature = new { value = 18m }, textDescription = "Sunny", timestamp = "2026-08-31T16:00:00Z" } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json<T>(T value) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value),
        });
    }
}
