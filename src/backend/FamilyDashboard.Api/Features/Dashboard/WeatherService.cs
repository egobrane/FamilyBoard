using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Dashboard;

internal sealed class WeatherService(
    FamilyDashboardDbContext dbContext,
    IWeatherProvider provider,
    IOptions<WeatherConfiguration> options)
{
    public Task<HouseholdWeatherConfiguration?> GetSettingsAsync(Guid householdId, CancellationToken cancellationToken) =>
        dbContext.HouseholdWeatherConfigurations.AsNoTracking().SingleOrDefaultAsync(value => value.HouseholdId == householdId, cancellationToken);

    public async Task<WeatherSettingsResponse> UpsertSettingsAsync(Guid householdId, UpdateWeatherSettingsRequest request, CancellationToken cancellationToken)
    {
        var value = await dbContext.HouseholdWeatherConfigurations.SingleOrDefaultAsync(candidate => candidate.HouseholdId == householdId, cancellationToken);
        if (value is null)
        {
            if (request.ExpectedVersion is not null and not 1) throw new DbUpdateConcurrencyException();
            value = new HouseholdWeatherConfiguration { HouseholdId = householdId, LocationLabel = request.LocationLabel!.Trim() };
            dbContext.Add(value);
        }
        else if (request.ExpectedVersion != value.Version) throw new DbUpdateConcurrencyException();
        value.Latitude = decimal.Round(request.Latitude, 4);
        value.Longitude = decimal.Round(request.Longitude, 4);
        value.LocationLabel = request.LocationLabel!.Trim();
        value.TemperatureUnit = request.TemperatureUnit!;
        value.Version++;
        value.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(value);
    }

    public async Task<bool> DeleteSettingsAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var value = await dbContext.HouseholdWeatherConfigurations.SingleOrDefaultAsync(candidate => candidate.HouseholdId == householdId, cancellationToken);
        if (value is null) return false;
        dbContext.Remove(value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<HouseholdWeatherResponse?> GetWeatherAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(householdId, cancellationToken);
        if (settings is null) return null;
        if (!options.Value.Enabled) throw new WeatherUnavailableException();
        var unit = settings.TemperatureUnit == "auto"
            ? await ResolveAutomaticUnitAsync(householdId, cancellationToken)
            : settings.TemperatureUnit;
        var result = await provider.GetAsync(settings.Latitude, settings.Longitude, unit, cancellationToken);
        return new(result.IsStale ? "stale" : "fresh", settings.LocationLabel, unit,
            result.Current, result.Forecast, result.ObservedAt, result.RetrievedAt, result.IsStale, result.Attribution);
    }

    public static WeatherSettingsResponse Map(HouseholdWeatherConfiguration value) =>
        new(value.HouseholdId, value.Latitude, value.Longitude, value.LocationLabel, value.TemperatureUnit, value.Version);

    private async Task<string> ResolveAutomaticUnitAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var locale = await dbContext.HouseholdConfigurations.AsNoTracking()
            .Where(value => value.HouseholdId == householdId)
            .Select(value => value.Locale)
            .SingleAsync(cancellationToken);
        var region = locale.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToUpperInvariant();
        return region is "US" or "LR" or "MM" ? "fahrenheit" : "celsius";
    }
}

internal sealed class WeatherUnavailableException : Exception;
