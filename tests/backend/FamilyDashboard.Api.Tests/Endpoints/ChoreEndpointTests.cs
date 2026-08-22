using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Chores;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class ChoreEndpointTests
{
    [PostgreSqlFact]
    public async Task CrossHouseholdAccessAndLockedSharedDisplayAdministrationFailClosed()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var first = new UserAccount { DisplayName = "First Adult", PrimaryEmail = "first-chores@example.test" };
        var second = new UserAccount { DisplayName = "Second Adult", PrimaryEmail = "second-chores@example.test" };
        database.DbContext.UserAccounts.AddRange(first, second);
        await database.DbContext.SaveChangesAsync();
        using var firstClient = Client(database.Factory, first.Id);
        using var secondClient = Client(database.Factory, second.Id);
        var firstHousehold = await BootstrapAsync(firstClient);
        var secondHousehold = await BootstrapAsync(secondClient);

        using var isolated = await secondClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/chore-assignments");
        Assert.Equal(HttpStatusCode.NotFound, isolated.StatusCode);
        Assert.Equal(ApiProblemCodes.HouseholdNotFound, await ProblemCodeAsync(isolated));

        var now = DateTimeOffset.UtcNow;
        var session = new UserSession
        {
            UserAccountId = first.Id,
            SelectedHouseholdId = firstHousehold.Id,
            IsSharedDisplay = true,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(2),
        };
        database.DbContext.UserSessions.Add(session);
        await database.DbContext.SaveChangesAsync();
        using var sharedClient = Client(database.Factory, first.Id, session.Id);
        using var routine = await sharedClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/chore-assignments");
        Assert.Equal(HttpStatusCode.OK, routine.StatusCode);
        using var administration = await sharedClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/chore-definitions");
        Assert.Equal(HttpStatusCode.Forbidden, administration.StatusCode);
        Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(administration));
        Assert.NotEqual(firstHousehold.Id, secondHousehold.Id);
    }

    [PostgreSqlFact]
    public async Task DefinitionAssignmentCompletionRejectionAndApprovalRetainHistory()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Chore Adult", PrimaryEmail = "chores@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var client = database.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, account.Id.ToString());
        var household = await BootstrapAsync(client);
        var childResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Alex", "mint"));
        var child = (await childResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;

        var definitionResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-definitions",
            new CreateChoreDefinitionRequest(Guid.NewGuid(), "Feed Milo", "Before dinner"));
        Assert.Equal(HttpStatusCode.Created, definitionResponse.StatusCode);
        var definition = (await definitionResponse.Content.ReadFromJsonAsync<ChoreDefinitionResponse>())!;

        var assignmentRequestId = Guid.NewGuid();
        var assignmentResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-assignments",
            new CreateChoreAssignmentRequest(assignmentRequestId, definition.Id, child.Id,
                new DateOnly(2026, 8, 23), new TimeOnly(18, 0)));
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        var assignment = (await assignmentResponse.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;

        var assignmentKeyReuse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-assignments",
            new CreateChoreAssignmentRequest(assignmentRequestId, definition.Id, child.Id,
                new DateOnly(2026, 8, 24), new TimeOnly(18, 0)));
        Assert.Equal(HttpStatusCode.Conflict, assignmentKeyReuse.StatusCode);

        var requestId = Guid.NewGuid();
        var completionResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-assignments/{assignment.Id}/completions",
            new CompleteChoreRequest(requestId, assignment.Version, child.Id));
        Assert.Equal(HttpStatusCode.OK, completionResponse.StatusCode);
        var completion = (await completionResponse.Content.ReadFromJsonAsync<ChoreCompletionResponse>())!;

        var replay = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-assignments/{assignment.Id}/completions",
            new CompleteChoreRequest(requestId, assignment.Version, child.Id));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(completion.Id, (await replay.Content.ReadFromJsonAsync<ChoreCompletionResponse>())!.Id);

        var completionKeyReuse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-assignments/{assignment.Id}/completions",
            new CompleteChoreRequest(requestId, assignment.Version, household.Access.MemberId));
        Assert.Equal(HttpStatusCode.Conflict, completionKeyReuse.StatusCode);

        var rejectedResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-completions/{completion.Id}/review",
            new ReviewChoreCompletionRequest(completion.Version, "rejected", "Please finish the water bowl."));
        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);

        var active = (await client.GetFromJsonAsync<ChoreListResponse>(
            $"/api/households/{household.Id}/chore-assignments?view=active"))!.Items.Single();
        Assert.Equal("pending", active.Status);
        var secondResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-assignments/{assignment.Id}/completions",
            new CompleteChoreRequest(Guid.NewGuid(), active.Version, child.Id));
        var second = (await secondResponse.Content.ReadFromJsonAsync<ChoreCompletionResponse>())!;
        var approvedResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-completions/{second.Id}/review",
            new ReviewChoreCompletionRequest(second.Version, "approved", null));
        Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);

        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(2, await database.DbContext.ChoreCompletions.CountAsync());
        Assert.Equal("Completed", await database.DbContext.ChoreAssignments
            .Where(item => item.Id == assignment.Id).Select(item => item.Status.ToString()).SingleAsync());
        Assert.Empty(database.DbContext.PointTransactions);
    }

    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/households",
            new CreateHouseholdRequest("Chore Family", "America/New_York", "en-US", "Sunday"));
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
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

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }
}
