using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Features.Points;
using FamilyDashboard.Api.Features.Rewards;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class RewardEndpointTests
{
    [PostgreSqlFact]
    public async Task CatalogReturnsActiveRewardsAndActiveMemberBalances()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Catalog Adult", PrimaryEmail = "reward-catalog@example.test" };
        database.DbContext.UserAccounts.Add(account); await database.DbContext.SaveChangesAsync();
        using var client = Client(database.Factory, account.Id);
        var household = await Bootstrap(client);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-adjustments",
            new CreatePointAdjustmentRequest(Guid.NewGuid(), household.Access.MemberId, 75, "Starting points"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/reward-definitions",
            new CreateRewardRequest(Guid.NewGuid(), "Movie night", "Choose the movie", 40))).StatusCode);

        var catalog = await client.GetFromJsonAsync<RewardCatalogResponse>($"/api/households/{household.Id}/rewards");

        var reward = Assert.Single(catalog!.Rewards);
        Assert.Equal("Movie night", reward.Title);
        Assert.Equal(40, reward.PointCost);
        var member = Assert.Single(catalog.Members);
        Assert.Equal(household.Access.MemberId, member.MemberId);
        Assert.Equal(75, member.Balance);
        Assert.True(member.IsActive);
    }

    [PostgreSqlFact]
    public async Task RedemptionReservesPointsAndRejectionRestoresThemAppendOnly()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Reward Adult", PrimaryEmail = "reward@example.test" };
        database.DbContext.UserAccounts.Add(account); await database.DbContext.SaveChangesAsync();
        using var bootstrapClient = Client(database.Factory, account.Id);
        var household = await Bootstrap(bootstrapClient);
        var now = DateTimeOffset.UtcNow;
        var session = new UserSession { UserAccountId = account.Id, SelectedHouseholdId = household.Id,
            CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddDays(1), AbsoluteExpiresAt = now.AddDays(2) };
        database.DbContext.UserSessions.Add(session); await database.DbContext.SaveChangesAsync();
        using var client = Client(database.Factory, account.Id, session.Id);
        var adjustment = await client.PostAsJsonAsync($"/api/households/{household.Id}/point-adjustments",
            new CreatePointAdjustmentRequest(Guid.NewGuid(), household.Access.MemberId, 100, "Starting points"));
        Assert.Equal(HttpStatusCode.Created, adjustment.StatusCode);

        var create = new CreateRewardRequest(Guid.NewGuid(), "Movie night", "Choose the movie", 40);
        var createdResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/reward-definitions", create);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var reward = (await createdResponse.Content.ReadFromJsonAsync<RewardResponse>())!;

        var request = new CreateRewardRedemptionRequest(Guid.NewGuid(), reward.Id, household.Access.MemberId);
        var requestedResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/reward-redemptions", request);
        Assert.Equal(HttpStatusCode.Created, requestedResponse.StatusCode);
        var redemption = (await requestedResponse.Content.ReadFromJsonAsync<RewardRedemptionResponse>())!;
        Assert.Equal("requested", redemption.Status); Assert.Equal(40, redemption.PointCost);

        var afterRequest = await client.GetFromJsonAsync<HouseholdPointSummaryResponse>($"/api/households/{household.Id}/points/summary");
        Assert.Equal(60, afterRequest!.Members.Single(x => x.MemberId == household.Access.MemberId).Balance);
        var debit = await database.DbContext.PointTransactions.SingleAsync(x => x.RewardRedemptionId == redemption.Id);
        Assert.Equal(-40, debit.Amount); Assert.Equal(PointTransactionType.RewardRedemption, debit.Type);

        var rejectedResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/reward-redemptions/{redemption.Id}/review",
            new ReviewRewardRedemptionRequest(redemption.Version, "rejected", "Not this week"));
        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);
        var afterReject = await client.GetFromJsonAsync<HouseholdPointSummaryResponse>($"/api/households/{household.Id}/points/summary");
        Assert.Equal(100, afterReject!.Members.Single(x => x.MemberId == household.Access.MemberId).Balance);
        Assert.Equal(3, await database.DbContext.PointTransactions.CountAsync());
        Assert.Contains(await database.DbContext.PointTransactions.ToListAsync(), x =>
            x.Type == PointTransactionType.Reversal && x.ReversesPointTransactionId == debit.Id && x.Amount == 40);
    }

    [PostgreSqlFact]
    public async Task RedemptionRejectsInsufficientBalanceAndIsHouseholdIsolated()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var first = new UserAccount { DisplayName = "First", PrimaryEmail = "reward-first@example.test" };
        var second = new UserAccount { DisplayName = "Second", PrimaryEmail = "reward-second@example.test" };
        database.DbContext.UserAccounts.AddRange(first, second); await database.DbContext.SaveChangesAsync();
        using var firstClient = Client(database.Factory, first.Id); using var secondClient = Client(database.Factory, second.Id);
        var household = await Bootstrap(firstClient); _ = await Bootstrap(secondClient);
        var rewardResponse = await firstClient.PostAsJsonAsync($"/api/households/{household.Id}/reward-definitions",
            new CreateRewardRequest(Guid.NewGuid(), "Late bedtime", null, 25));
        var reward = (await rewardResponse.Content.ReadFromJsonAsync<RewardResponse>())!;

        using var isolated = await secondClient.GetAsync($"/api/households/{household.Id}/rewards");
        Assert.Equal(HttpStatusCode.NotFound, isolated.StatusCode);

        var now = DateTimeOffset.UtcNow;
        var session = new UserSession { UserAccountId = first.Id, SelectedHouseholdId = household.Id,
            CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddDays(1), AbsoluteExpiresAt = now.AddDays(2) };
        database.DbContext.UserSessions.Add(session); await database.DbContext.SaveChangesAsync();
        using var requesting = Client(database.Factory, first.Id, session.Id);
        var insufficient = await requesting.PostAsJsonAsync($"/api/households/{household.Id}/reward-redemptions",
            new CreateRewardRedemptionRequest(Guid.NewGuid(), reward.Id, household.Access.MemberId));
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
        Assert.Equal(ApiProblemCodes.RewardInsufficientPoints, await ProblemCode(insufficient));
        Assert.Empty(await database.DbContext.RewardRedemptions.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task ConcurrentRequestsCannotOverspendOneMemberBalance()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Concurrent Adult", PrimaryEmail = "reward-concurrent@example.test" };
        database.DbContext.UserAccounts.Add(account); await database.DbContext.SaveChangesAsync();
        using var bootstrapClient = Client(database.Factory, account.Id); var household = await Bootstrap(bootstrapClient);
        var now = DateTimeOffset.UtcNow;
        var sessions = new[] { new UserSession { UserAccountId = account.Id, SelectedHouseholdId = household.Id,
                CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddDays(1), AbsoluteExpiresAt = now.AddDays(2) },
            new UserSession { UserAccountId = account.Id, SelectedHouseholdId = household.Id,
                CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddDays(1), AbsoluteExpiresAt = now.AddDays(2) } };
        database.DbContext.UserSessions.AddRange(sessions); await database.DbContext.SaveChangesAsync();
        using var first = Client(database.Factory, account.Id, sessions[0].Id);
        using var second = Client(database.Factory, account.Id, sessions[1].Id);
        Assert.Equal(HttpStatusCode.Created, (await first.PostAsJsonAsync($"/api/households/{household.Id}/point-adjustments",
            new CreatePointAdjustmentRequest(Guid.NewGuid(), household.Access.MemberId, 50, "Starting points"))).StatusCode);
        var reward = (await (await first.PostAsJsonAsync($"/api/households/{household.Id}/reward-definitions",
            new CreateRewardRequest(Guid.NewGuid(), "Family outing", null, 40))).Content.ReadFromJsonAsync<RewardResponse>())!;

        var responses = await Task.WhenAll(first.PostAsJsonAsync($"/api/households/{household.Id}/reward-redemptions",
            new CreateRewardRedemptionRequest(Guid.NewGuid(), reward.Id, household.Access.MemberId)),
            second.PostAsJsonAsync($"/api/households/{household.Id}/reward-redemptions",
                new CreateRewardRedemptionRequest(Guid.NewGuid(), reward.Id, household.Access.MemberId)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await database.DbContext.PointTransactions.CountAsync(x => x.Type == PointTransactionType.RewardRedemption));
        var summary = await first.GetFromJsonAsync<HouseholdPointSummaryResponse>($"/api/households/{household.Id}/points/summary");
        Assert.Equal(10, summary!.Members.Single(x => x.MemberId == household.Access.MemberId).Balance);
    }

    private static async Task<HouseholdResponse> Bootstrap(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/households",
            new CreateHouseholdRequest($"Reward {Guid.NewGuid():N}", "America/New_York", "en-US", "Sunday"));
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }
    private static HttpClient Client(IdentityHouseholdWebApplicationFactory factory, Guid accountId, Guid? sessionId = null)
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, accountId.ToString());
        if (sessionId is not null) client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SessionIdHeaderName, sessionId.ToString());
        return client;
    }
    private static async Task<string?> ProblemCode(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.GetProperty("code").GetString();
    }
}
