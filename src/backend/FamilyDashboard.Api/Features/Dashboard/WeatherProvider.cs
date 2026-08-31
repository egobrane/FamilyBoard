using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FamilyDashboard.Api.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Dashboard;

public interface IWeatherProvider
{
    Task<ProviderWeatherResult> GetAsync(decimal latitude, decimal longitude, string temperatureUnit, CancellationToken cancellationToken);
}

public sealed record ProviderWeatherResult(
    WeatherCurrentResponse? Current,
    IReadOnlyList<WeatherPeriodResponse> Forecast,
    DateTimeOffset? ObservedAt,
    DateTimeOffset RetrievedAt,
    bool IsStale,
    string Attribution);

internal sealed class NwsWeatherProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IOptions<WeatherConfiguration> options) : IWeatherProvider
{
    public async Task<ProviderWeatherResult> GetAsync(decimal latitude, decimal longitude, string temperatureUnit, CancellationToken cancellationToken)
    {
        var key = $"weather:{latitude:F4}:{longitude:F4}:{temperatureUnit}";
        if (cache.TryGetValue<WeatherCacheEntry>(key, out var cached) && cached!.FreshUntil > DateTimeOffset.UtcNow)
            return cached.Value;
        try
        {
            var value = await RetrieveAsync(latitude, longitude, temperatureUnit, cancellationToken);
            cache.Set(key, new WeatherCacheEntry(value, DateTimeOffset.UtcNow.Add(options.Value.FreshLifetime)), options.Value.StaleLifetime);
            return value;
        }
        catch when (cached is not null && cached.Value.RetrievedAt.Add(options.Value.StaleLifetime) > DateTimeOffset.UtcNow)
        {
            return cached.Value with { IsStale = true };
        }
    }

    private async Task<ProviderWeatherResult> RetrieveAsync(decimal latitude, decimal longitude, string temperatureUnit, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(NwsWeatherProvider));
        var points = await GetAsync<NwsPoints>(client, $"points/{latitude:F4},{longitude:F4}", cancellationToken);
        var properties = points.Properties ?? throw new WeatherProviderException("The weather provider returned no grid information.");
        var forecastUrl = temperatureUnit == "celsius" ? AddUnits(properties.Forecast, "si") : properties.Forecast;
        var forecast = await GetAbsoluteAsync<NwsForecast>(client, forecastUrl, cancellationToken);
        WeatherCurrentResponse? current = null;
        DateTimeOffset? observedAt = null;
        if (!string.IsNullOrWhiteSpace(properties.ObservationStations))
        {
            var stations = await GetAbsoluteAsync<NwsStations>(client, properties.ObservationStations, cancellationToken);
            var station = stations.Features is { Count: > 0 } features ? features[0].Id : null;
            if (!string.IsNullOrWhiteSpace(station))
            {
                var observation = await GetAbsoluteAsync<NwsObservation>(client, $"{station}/observations/latest", cancellationToken);
                var o = observation.Properties;
                var celsius = o?.Temperature?.Value;
                decimal? temperature = celsius is null ? null : temperatureUnit == "celsius"
                    ? Math.Round(celsius.Value, 0) : Math.Round(celsius.Value * 9 / 5 + 32, 0);
                current = new(temperature, o?.TextDescription ?? "Current conditions", WeatherIcon(o?.TextDescription));
                observedAt = o?.Timestamp;
            }
        }
        var periods = forecast.Properties?.Periods?.Take(14).Select(period => new WeatherPeriodResponse(
            period.Name ?? "Forecast", period.StartTime, period.EndTime, period.Temperature,
            NormalizeUnit(period.TemperatureUnit), period.ShortForecast ?? "Forecast unavailable",
            WeatherIcon(period.ShortForecast), period.IsDaytime)).ToArray() ?? [];
        return new(current, periods, observedAt, DateTimeOffset.UtcNow, false, "Weather data from the National Weather Service");
    }

    private async Task<T> GetAsync<T>(HttpClient client, string path, CancellationToken cancellationToken) =>
        await GetAbsoluteAsync<T>(client, new Uri(new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"), path).ToString(), cancellationToken);

    private static async Task<T> GetAbsoluteAsync<T>(HttpClient client, string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new WeatherProviderRateLimitedException();
                if ((response.StatusCode == HttpStatusCode.RequestTimeout || (int)response.StatusCode >= 500) && attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new WeatherProviderException($"Weather provider returned HTTP {(int)response.StatusCode}.");
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                    ?? throw new WeatherProviderException("Weather provider returned an empty response.");
            }
            catch (HttpRequestException) when (attempt == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }
        }
        throw new WeatherProviderException("Weather provider did not recover after one retry.");
    }

    private static string AddUnits(string url, string units) => url.Contains('?') ? $"{url}&units={units}" : $"{url}?units={units}";
    private static string NormalizeUnit(string? value) => value?.ToUpperInvariant() == "C" ? "celsius" : "fahrenheit";
    private static string WeatherIcon(string? text)
    {
        var value = text?.ToLowerInvariant() ?? string.Empty;
        if (value.Contains("thunder")) return "thunderstorm";
        if (value.Contains("snow") || value.Contains("sleet")) return "snow";
        if (value.Contains("rain") || value.Contains("shower")) return "rain";
        if (value.Contains("cloud") || value.Contains("overcast")) return "cloudy";
        if (value.Contains("fog") || value.Contains("haze")) return "fog";
        return "clear";
    }

    private sealed record WeatherCacheEntry(ProviderWeatherResult Value, DateTimeOffset FreshUntil);
    private sealed record NwsPoints([property: JsonPropertyName("properties")] NwsPointProperties? Properties);
    private sealed record NwsPointProperties(string Forecast, string ObservationStations);
    private sealed record NwsForecast([property: JsonPropertyName("properties")] NwsForecastProperties? Properties);
    private sealed record NwsForecastProperties(IReadOnlyList<NwsPeriod>? Periods);
    private sealed record NwsPeriod(string? Name, DateTimeOffset StartTime, DateTimeOffset EndTime, decimal? Temperature, string? TemperatureUnit, string? ShortForecast, bool IsDaytime);
    private sealed record NwsStations(IReadOnlyList<NwsStation>? Features);
    private sealed record NwsStation([property: JsonPropertyName("id")] string? Id);
    private sealed record NwsObservation([property: JsonPropertyName("properties")] NwsObservationProperties? Properties);
    private sealed record NwsObservationProperties(NwsMeasure? Temperature, string? TextDescription, DateTimeOffset? Timestamp);
    private sealed record NwsMeasure(decimal? Value);
}

internal class WeatherProviderException(string message) : Exception(message);
internal sealed class WeatherProviderRateLimitedException() : WeatherProviderException("Weather provider rate limit reached.");
