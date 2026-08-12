using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class AuthenticationEndpointTests
{
    [PostgreSqlFact]
    public async Task CurrentUserRequiresAuthenticationAndAnActivePersistedAccount()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var client = database.Factory.CreateClient();

        using var anonymousResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(
            ApiProblemCodes.AuthenticationRequired,
            await ReadProblemCodeAsync(anonymousResponse));

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, Guid.NewGuid().ToString());
        using var missingAccountResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, missingAccountResponse.StatusCode);
        Assert.Equal(
            ApiProblemCodes.AccountUnavailable,
            await ReadProblemCodeAsync(missingAccountResponse));
    }

    [PostgreSqlFact]
    public async Task CurrentUserReturnsAccountAndAllActiveHouseholdMemberships()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount
        {
            DisplayName = "Alex Adult",
            PrimaryEmail = "alex@example.test",
        };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();

        using var client = database.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, account.Id.ToString());
        await CreateHouseholdAsync(client, "First Household");
        await CreateHouseholdAsync(client, "Second Household");

        using var response = await client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(currentUser);
        Assert.Equal(account.Id, currentUser.User.Id);
        Assert.Equal("Alex Adult", currentUser.User.DisplayName);
        Assert.Equal(2, currentUser.Households.Count);
        Assert.All(currentUser.Households, household => Assert.Equal("adult", household.Role));
        Assert.Null(currentUser.SelectedHouseholdId);
    }

    private static async Task CreateHouseholdAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/households",
            new
            {
                name,
                timeZone = "America/New_York",
                locale = "en-US",
                weekStartsOn = "Sunday",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
