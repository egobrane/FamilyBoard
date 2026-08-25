using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Features.Points;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class PointEndpointTests
{
    [PostgreSqlFact]
    public async Task AdjustmentsDeriveBalancesAndReversalsRemainAppendOnlyAndIdempotent()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Point Adult", PrimaryEmail = "points@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var client = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(client, "Point Family");
        var childResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Alex", "mint"));
        var child = (await childResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;

        var requestId = Guid.NewGuid();
        var adjustmentRequest = new CreatePointAdjustmentRequest(requestId, child.Id, 25, "Starting balance");
        var adjustmentResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-adjustments", adjustmentRequest);
        Assert.Equal(HttpStatusCode.Created, adjustmentResponse.StatusCode);
        var adjustment = (await adjustmentResponse.Content.ReadFromJsonAsync<PointTransactionResponse>())!;

        var replay = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-adjustments", adjustmentRequest);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(adjustment.Id, (await replay.Content.ReadFromJsonAsync<PointTransactionResponse>())!.Id);

        var conflict = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-adjustments", adjustmentRequest with { Amount = 30 });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(ApiProblemCodes.PointIdempotencyConflict, await ProblemCodeAsync(conflict));

        var deactivationResponse = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/members/{child.Id}",
            new UpdateHouseholdMemberRequest(null, null, false));
        Assert.Equal(HttpStatusCode.OK, deactivationResponse.StatusCode);

        var reversalRequest = new ReversePointTransactionRequest(Guid.NewGuid(), "Entered for testing");
        var reversalResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-transactions/{adjustment.Id}/reverse", reversalRequest);
        Assert.Equal(HttpStatusCode.Created, reversalResponse.StatusCode);
        var reversal = (await reversalResponse.Content.ReadFromJsonAsync<PointTransactionResponse>())!;
        Assert.Equal(-25, reversal.Amount);
        Assert.Equal(adjustment.Id, reversal.ReversesPointTransactionId);

        var secondReversal = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-transactions/{adjustment.Id}/reverse",
            new ReversePointTransactionRequest(Guid.NewGuid(), "Again"));
        Assert.Equal(HttpStatusCode.Conflict, secondReversal.StatusCode);
        Assert.Equal(ApiProblemCodes.PointTransactionAlreadyReversed, await ProblemCodeAsync(secondReversal));

        var summary = await client.GetFromJsonAsync<HouseholdPointSummaryResponse>(
            $"/api/households/{household.Id}/points/summary");
        Assert.NotNull(summary);
        Assert.Equal(0, summary.HouseholdBalance);
        var inactiveChildBalance = summary.Members.Single(item => item.MemberId == child.Id);
        Assert.False(inactiveChildBalance.IsActive);
        Assert.Equal(0, inactiveChildBalance.Balance);
        Assert.Equal(2, await database.DbContext.PointTransactions.CountAsync());
    }

    [PostgreSqlFact]
    public async Task PointReadsAreHouseholdIsolatedAndSharedDisplayCorrectionsRequireElevation()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var first = new UserAccount { DisplayName = "First", PrimaryEmail = "first-points@example.test" };
        var second = new UserAccount { DisplayName = "Second", PrimaryEmail = "second-points@example.test" };
        database.DbContext.UserAccounts.AddRange(first, second);
        await database.DbContext.SaveChangesAsync();
        using var firstClient = Client(database.Factory, first.Id);
        using var secondClient = Client(database.Factory, second.Id);
        var firstHousehold = await BootstrapAsync(firstClient, "First Points");
        _ = await BootstrapAsync(secondClient, "Second Points");

        using var isolated = await secondClient.GetAsync($"/api/households/{firstHousehold.Id}/points/summary");
        Assert.Equal(HttpStatusCode.NotFound, isolated.StatusCode);

        var now = DateTimeOffset.UtcNow;
        var session = new UserSession
        {
            UserAccountId = first.Id, SelectedHouseholdId = firstHousehold.Id, IsSharedDisplay = true,
            CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddDays(1), AbsoluteExpiresAt = now.AddDays(2),
        };
        database.DbContext.UserSessions.Add(session);
        await database.DbContext.SaveChangesAsync();
        using var shared = Client(database.Factory, first.Id, session.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await shared.GetAsync($"/api/households/{firstHousehold.Id}/points/summary")).StatusCode);
        var correction = await shared.PostAsJsonAsync($"/api/households/{firstHousehold.Id}/point-adjustments",
            new CreatePointAdjustmentRequest(Guid.NewGuid(), firstHousehold.Access.MemberId, 5, "Test"));
        Assert.Equal(HttpStatusCode.Forbidden, correction.StatusCode);
        Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(correction));
    }

    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/households",
            new CreateHouseholdRequest(name, "America/New_York", "en-US", "Sunday"));
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    private static HttpClient Client(IdentityHouseholdWebApplicationFactory factory,
        Guid accountId, Guid? sessionId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, accountId.ToString());
        if (sessionId is not null)
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SessionIdHeaderName, sessionId.ToString());
        return client;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }
}
