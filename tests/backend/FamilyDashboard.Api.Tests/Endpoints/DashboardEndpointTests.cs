using System.Net;
using System.Net.Http.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Dashboard;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class DashboardEndpointTests
{
    [PostgreSqlFact]
    public async Task AppearanceAndWeatherSettingsPersistForTheAuthorizedHousehold()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Dashboard Adult", PrimaryEmail = "dashboard@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var client = Client(database, account.Id);
        var household = await BootstrapAsync(client, "Dashboard Household");

        var appearanceResponse = await client.PutAsJsonAsync(
            $"/api/households/{household.Id}/dashboard-appearance",
            new UpdateDashboardAppearanceRequest("Welcome home", "Dinner is at six.", 0.25m, 0.75m, 1));
        Assert.Equal(HttpStatusCode.OK, appearanceResponse.StatusCode);
        var appearance = await appearanceResponse.Content.ReadFromJsonAsync<DashboardAppearanceResponse>();
        Assert.Equal("Welcome home", appearance!.GreetingTitle);
        Assert.Equal(0.25m, appearance.PhotoFocalX);

        var weatherResponse = await client.PutAsJsonAsync(
            $"/api/households/{household.Id}/weather-settings",
            new UpdateWeatherSettingsRequest(40.712812m, -74.006012m, "Near home", "auto", null));
        Assert.Equal(HttpStatusCode.OK, weatherResponse.StatusCode);
        var weather = await weatherResponse.Content.ReadFromJsonAsync<WeatherSettingsResponse>();
        Assert.Equal(40.7128m, weather!.Latitude);
        Assert.Equal(-74.0060m, weather.Longitude);

        database.DbContext.ChangeTracker.Clear();
        Assert.Equal("Dinner is at six.", await database.DbContext.HouseholdDashboardAppearances
            .Where(value => value.HouseholdId == household.Id).Select(value => value.GreetingMessage).SingleAsync());
        Assert.Equal("Near home", await database.DbContext.HouseholdWeatherConfigurations
            .Where(value => value.HouseholdId == household.Id).Select(value => value.LocationLabel).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task DashboardSettingsRemainHouseholdIsolated()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var first = new UserAccount { DisplayName = "First Adult", PrimaryEmail = "dashboard-first@example.test" };
        var second = new UserAccount { DisplayName = "Second Adult", PrimaryEmail = "dashboard-second@example.test" };
        database.DbContext.UserAccounts.AddRange(first, second);
        await database.DbContext.SaveChangesAsync();
        using var firstClient = Client(database, first.Id);
        using var secondClient = Client(database, second.Id);
        _ = await BootstrapAsync(firstClient, "First Household");
        var secondHousehold = await BootstrapAsync(secondClient, "Second Household");

        using var response = await firstClient.PutAsJsonAsync(
            $"/api/households/{secondHousehold.Id}/weather-settings",
            new UpdateWeatherSettingsRequest(40, -74, "Not allowed", "fahrenheit", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await database.DbContext.HouseholdWeatherConfigurations
            .AnyAsync(value => value.HouseholdId == secondHousehold.Id));
    }

    private static HttpClient Client(PostgreSqlTestDatabase database, Guid accountId)
    {
        var client = database.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, accountId.ToString());
        return client;
    }

    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/households",
            new CreateHouseholdRequest(name, "America/New_York", "en-US", "Sunday"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }
}
