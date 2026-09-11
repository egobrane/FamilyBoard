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
    public async Task AdditionDefinitionsAreVersionedAndApplicationsSnapshotAndReplaySafely()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Addition Adult", PrimaryEmail = "additions@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var client = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(client, "Addition Family");
        var childResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Alex", "mint"));
        var child = (await childResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;

        var definitionRequest = new CreatePointAdditionDefinitionRequest(
            Guid.NewGuid(), "Homework completed on time", 100);
        var createResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions", definitionRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var definition = (await createResponse.Content
            .ReadFromJsonAsync<PointAdditionDefinitionResponse>())!;

        var replayCreate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions", definitionRequest);
        Assert.Equal(definition.Id, (await replayCreate.Content
            .ReadFromJsonAsync<PointAdditionDefinitionResponse>())!.Id);
        var conflictingCreate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions",
            definitionRequest with { AdditionAmount = 75 });
        Assert.Equal(HttpStatusCode.Conflict, conflictingCreate.StatusCode);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}",
            new UpdatePointAdditionDefinitionRequest(definition.Version, "Homework on time", 100));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        definition = (await updateResponse.Content.ReadFromJsonAsync<PointAdditionDefinitionResponse>())!;

        var applicationRequest = new ApplyPointAdditionRequest(
            Guid.NewGuid(), child.Id, definition.Version);
        var applicationResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}/applications",
            applicationRequest);
        Assert.Equal(HttpStatusCode.Created, applicationResponse.StatusCode);
        var application = (await applicationResponse.Content.ReadFromJsonAsync<PointTransactionResponse>())!;
        Assert.Equal(100, application.Amount);
        Assert.Equal("Homework on time", application.Description);
        Assert.Equal(definition.Id, application.PointAdditionDefinitionId);

        var secondUpdate = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}",
            new UpdatePointAdditionDefinitionRequest(definition.Version, "Great homework", 125));
        definition = (await secondUpdate.Content.ReadFromJsonAsync<PointAdditionDefinitionResponse>())!;
        var replayApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}/applications",
            applicationRequest);
        Assert.Equal(application.Id, (await replayApplication.Content
            .ReadFromJsonAsync<PointTransactionResponse>())!.Id);

        var nextApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}/applications",
            new ApplyPointAdditionRequest(Guid.NewGuid(), child.Id, definition.Version));
        var nextTransaction = (await nextApplication.Content.ReadFromJsonAsync<PointTransactionResponse>())!;
        Assert.Equal(125, nextTransaction.Amount);
        Assert.Equal("Great homework", nextTransaction.Description);

        var deactivate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}/deactivate",
            new ChangePointAdditionDefinitionStateRequest(definition.Version));
        definition = (await deactivate.Content.ReadFromJsonAsync<PointAdditionDefinitionResponse>())!;
        var inactiveApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}/applications",
            new ApplyPointAdditionRequest(Guid.NewGuid(), child.Id, definition.Version));
        Assert.Equal(HttpStatusCode.Conflict, inactiveApplication.StatusCode);
        Assert.Equal(ApiProblemCodes.PointAdditionDefinitionInactive,
            await ProblemCodeAsync(inactiveApplication));

        var reactivate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}/activate",
            new ChangePointAdditionDefinitionStateRequest(definition.Version));
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        definition = (await reactivate.Content.ReadFromJsonAsync<PointAdditionDefinitionResponse>())!;
        var deactivateMember = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/members/{child.Id}",
            new UpdateHouseholdMemberRequest(null, null, false));
        Assert.Equal(HttpStatusCode.OK, deactivateMember.StatusCode);
        var inactiveMemberApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-addition-definitions/{definition.Id}/applications",
            new ApplyPointAdditionRequest(Guid.NewGuid(), child.Id, definition.Version));
        Assert.Equal(HttpStatusCode.Conflict, inactiveMemberApplication.StatusCode);
        Assert.Equal(ApiProblemCodes.PointMemberInactive,
            await ProblemCodeAsync(inactiveMemberApplication));

        var reversal = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-transactions/{application.Id}/reverse",
            new ReversePointTransactionRequest(Guid.NewGuid(), "Applied by mistake"));
        Assert.Equal(HttpStatusCode.Created, reversal.StatusCode);
        var summary = await client.GetFromJsonAsync<HouseholdPointSummaryResponse>(
            $"/api/households/{household.Id}/points/summary");
        Assert.Equal(125, summary!.Members.Single(item => item.MemberId == child.Id).Balance);
        Assert.Equal(1, await database.DbContext.PointAdditionDefinitions.CountAsync());
        Assert.Equal(3, await database.DbContext.PointTransactions.CountAsync());
    }

    [PostgreSqlFact]
    public async Task DeductionDefinitionsAreVersionedAndApplicationsSnapshotAndReplaySafely()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Deduction Adult", PrimaryEmail = "deductions@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var client = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(client, "Deduction Family");
        var childResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Alex", "mint"));
        var child = (await childResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;

        var definitionRequest = new CreatePointDeductionDefinitionRequest(
            Guid.NewGuid(), "Hitting someone", 500);
        var createResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions", definitionRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var definition = (await createResponse.Content
            .ReadFromJsonAsync<PointDeductionDefinitionResponse>())!;

        var replayCreate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions", definitionRequest);
        Assert.Equal(definition.Id, (await replayCreate.Content
            .ReadFromJsonAsync<PointDeductionDefinitionResponse>())!.Id);
        var conflictingCreate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions",
            definitionRequest with { DeductionAmount = 400 });
        Assert.Equal(HttpStatusCode.Conflict, conflictingCreate.StatusCode);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}",
            new UpdatePointDeductionDefinitionRequest(definition.Version, "Unsafe hands", 500));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        definition = (await updateResponse.Content.ReadFromJsonAsync<PointDeductionDefinitionResponse>())!;

        var applicationRequest = new ApplyPointDeductionRequest(
            Guid.NewGuid(), child.Id, definition.Version);
        var applicationResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}/applications",
            applicationRequest);
        Assert.Equal(HttpStatusCode.Created, applicationResponse.StatusCode);
        var application = (await applicationResponse.Content.ReadFromJsonAsync<PointTransactionResponse>())!;
        Assert.Equal(-500, application.Amount);
        Assert.Equal("Unsafe hands", application.Description);
        Assert.Equal(definition.Id, application.PointDeductionDefinitionId);

        var secondUpdate = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}",
            new UpdatePointDeductionDefinitionRequest(definition.Version, "Hands to yourself", 250));
        definition = (await secondUpdate.Content.ReadFromJsonAsync<PointDeductionDefinitionResponse>())!;
        var replayApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}/applications",
            applicationRequest);
        Assert.Equal(application.Id, (await replayApplication.Content
            .ReadFromJsonAsync<PointTransactionResponse>())!.Id);

        var nextApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}/applications",
            new ApplyPointDeductionRequest(Guid.NewGuid(), child.Id, definition.Version));
        var nextTransaction = (await nextApplication.Content.ReadFromJsonAsync<PointTransactionResponse>())!;
        Assert.Equal(-250, nextTransaction.Amount);
        Assert.Equal("Hands to yourself", nextTransaction.Description);

        var deactivate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}/deactivate",
            new ChangePointDeductionDefinitionStateRequest(definition.Version));
        definition = (await deactivate.Content.ReadFromJsonAsync<PointDeductionDefinitionResponse>())!;
        var inactiveApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}/applications",
            new ApplyPointDeductionRequest(Guid.NewGuid(), child.Id, definition.Version));
        Assert.Equal(HttpStatusCode.Conflict, inactiveApplication.StatusCode);
        Assert.Equal(ApiProblemCodes.PointDeductionDefinitionInactive,
            await ProblemCodeAsync(inactiveApplication));

        var reactivate = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}/activate",
            new ChangePointDeductionDefinitionStateRequest(definition.Version));
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        definition = (await reactivate.Content.ReadFromJsonAsync<PointDeductionDefinitionResponse>())!;
        var deactivateMember = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/members/{child.Id}",
            new UpdateHouseholdMemberRequest(null, null, false));
        Assert.Equal(HttpStatusCode.OK, deactivateMember.StatusCode);
        var inactiveMemberApplication = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-deduction-definitions/{definition.Id}/applications",
            new ApplyPointDeductionRequest(Guid.NewGuid(), child.Id, definition.Version));
        Assert.Equal(HttpStatusCode.Conflict, inactiveMemberApplication.StatusCode);
        Assert.Equal(ApiProblemCodes.PointMemberInactive,
            await ProblemCodeAsync(inactiveMemberApplication));

        var reversal = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/point-transactions/{application.Id}/reverse",
            new ReversePointTransactionRequest(Guid.NewGuid(), "Applied by mistake"));
        Assert.Equal(HttpStatusCode.Created, reversal.StatusCode);
        var summary = await client.GetFromJsonAsync<HouseholdPointSummaryResponse>(
            $"/api/households/{household.Id}/points/summary");
        Assert.Equal(-250, summary!.Members.Single(item => item.MemberId == child.Id).Balance);
        Assert.Equal(1, await database.DbContext.PointDeductionDefinitions.CountAsync());
        Assert.Equal(3, await database.DbContext.PointTransactions.CountAsync());
    }

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
        using var isolatedDefinitions = await secondClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/point-deduction-definitions");
        Assert.Equal(HttpStatusCode.NotFound, isolatedDefinitions.StatusCode);
        using var isolatedAdditions = await secondClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/point-addition-definitions");
        Assert.Equal(HttpStatusCode.NotFound, isolatedAdditions.StatusCode);

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
        var definitions = await shared.GetAsync(
            $"/api/households/{firstHousehold.Id}/point-deduction-definitions");
        Assert.Equal(HttpStatusCode.Forbidden, definitions.StatusCode);
        Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(definitions));
        var additions = await shared.GetAsync(
            $"/api/households/{firstHousehold.Id}/point-addition-definitions");
        Assert.Equal(HttpStatusCode.Forbidden, additions.StatusCode);
        Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(additions));
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
