using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Calendar;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class CalendarEndpointTests
{
    [PostgreSqlFact]
    public async Task ConnectionStatusIsUnavailableWhenFeatureIsDisabled()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Owner", "owner-calendar@example.test");
        using var client = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(client, "Calendar Household");

        var response = await client.GetFromJsonAsync<CalendarConnectionResponse>(
            $"/api/households/{household.Id}/calendar/connection");

        Assert.NotNull(response);
        Assert.False(response.IsAvailable);
        Assert.Equal("disconnected", response.Status);
    }

    [PostgreSqlFact]
    public async Task AuthorizationUsesSeparateReadOnlyCalendarScopes()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Owner", "scope-calendar@example.test");
        await using var factory = new IdentityHouseholdWebApplicationFactory(
            Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!, enableCalendar: true);
        using var client = Client(factory, account.Id);
        var household = await BootstrapAsync(client, "Scoped Calendar Household");

        using var response = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/calendar/authorization",
            new BeginCalendarAuthorizationRequest($"/households/{household.Id}/calendars"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BeginCalendarAuthorizationResponse>();
        Assert.NotNull(result);
        var decoded = Uri.UnescapeDataString(result.AuthorizationUrl);
        Assert.Contains(GoogleCalendarScopes.CalendarListReadOnly, decoded);
        Assert.Contains(GoogleCalendarScopes.EventsReadOnly, decoded);
        Assert.DoesNotContain("auth/calendar ", decoded);
        Assert.Contains("access_type=offline", result.AuthorizationUrl);
        var correlation = response.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith(CalendarCorrelationCookieService.CookieName, StringComparison.Ordinal));
        Assert.Contains("path=/api/integrations/google-calendar/callback", correlation,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", correlation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", correlation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", correlation, StringComparison.OrdinalIgnoreCase);
    }

    [PostgreSqlFact]
    public async Task EventCreationAuthorizationUsesIncrementalWriteScope()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Owner", "write-scope-calendar@example.test");
        await using var factory = new IdentityHouseholdWebApplicationFactory(
            Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!,
            enableCalendar: true,
            enableCalendarEventCreation: true);
        using var client = Client(factory, account.Id);
        var household = await BootstrapAsync(client, "Writable Calendar Household");

        using var response = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/calendar/authorization",
            new BeginCalendarAuthorizationRequest(
                $"/households/{household.Id}/calendars",
                CalendarAuthorizationCapabilities.EventCreation));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BeginCalendarAuthorizationResponse>();
        Assert.NotNull(result);
        var decoded = Uri.UnescapeDataString(result.AuthorizationUrl);
        Assert.Contains(GoogleCalendarScopes.EventsWrite, decoded);
        Assert.Contains(GoogleCalendarScopes.CalendarListReadOnly, decoded);
        Assert.DoesNotContain(GoogleCalendarScopes.EventsReadOnly, decoded);
    }

    [PostgreSqlFact]
    public async Task CrossHouseholdAndLockedSharedDisplayFailClosed()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var first = await AddAccountAsync(database, "First", "first-calendar@example.test");
        var second = await AddAccountAsync(database, "Second", "second-calendar@example.test");
        using var firstClient = Client(database.Factory, first.Id);
        using var secondClient = Client(database.Factory, second.Id);
        var firstHousehold = await BootstrapAsync(firstClient, "First Calendar Household");
        var secondHousehold = await BootstrapAsync(secondClient, "Second Calendar Household");

        using var crossResponse = await secondClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/calendar/connection");
        Assert.Equal(HttpStatusCode.NotFound, crossResponse.StatusCode);
        Assert.Equal(ApiProblemCodes.HouseholdNotFound, await ProblemCodeAsync(crossResponse));
        using var crossDisplaySettingsResponse = await secondClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/calendar/display-settings");
        Assert.Equal(HttpStatusCode.NotFound, crossDisplaySettingsResponse.StatusCode);

        var session = new UserSession
        {
            UserAccountId = first.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            AbsoluteExpiresAt = DateTimeOffset.UtcNow.AddDays(2),
            SelectedHouseholdId = firstHousehold.Id,
            IsSharedDisplay = true,
        };
        database.DbContext.UserSessions.Add(session);
        await database.DbContext.SaveChangesAsync();
        using var sharedClient = Client(database.Factory, first.Id, session.Id);
        using var lockedResponse = await sharedClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/calendar/connection");
        Assert.Equal(HttpStatusCode.Forbidden, lockedResponse.StatusCode);
        Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(lockedResponse));

        using var lockedManagementResponse = await sharedClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/calendar/managed-events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, lockedManagementResponse.StatusCode);
        Assert.Equal(ApiProblemCodes.ParentElevationRequired,
            await ProblemCodeAsync(lockedManagementResponse));

        using var routineResponse = await sharedClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/calendar/events?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, routineResponse.StatusCode);
        var displaySettings = await sharedClient.GetFromJsonAsync<CalendarDisplaySettingsResponse>(
            $"/api/households/{firstHousehold.Id}/calendar/display-settings");
        Assert.NotNull(displaySettings);
        Assert.Equal("America/New_York", displaySettings.TimeZone);
        Assert.Equal("en-US", displaySettings.Locale);
        Assert.Equal("sunday", displaySettings.WeekStartsOn);
        Assert.NotEqual(firstHousehold.Id, secondHousehold.Id);
    }

    private static async Task<UserAccount> AddAccountAsync(
        PostgreSqlTestDatabase database, string name, string email)
    {
        var account = new UserAccount { DisplayName = name, PrimaryEmail = email };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        return account;
    }

    private static HttpClient Client(
        IdentityHouseholdWebApplicationFactory factory, Guid accountId, Guid? sessionId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, accountId.ToString());
        if (sessionId is not null)
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SessionIdHeaderName, sessionId.ToString());
        return client;
    }

    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/households",
            new CreateHouseholdRequest(name, "America/New_York", "en-US", "Sunday"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }
}
