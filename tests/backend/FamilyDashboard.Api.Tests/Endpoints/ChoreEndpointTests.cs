using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Features.Chores;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        using var scheduleAdministration = await sharedClient.GetAsync(
            $"/api/households/{firstHousehold.Id}/chore-schedules");
        Assert.Equal(HttpStatusCode.Forbidden, scheduleAdministration.StatusCode);
        Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(scheduleAdministration));
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
            new CreateChoreDefinitionRequest(Guid.NewGuid(), "Feed Milo", "Before dinner", 15));
        Assert.Equal(HttpStatusCode.Created, definitionResponse.StatusCode);
        var definition = (await definitionResponse.Content.ReadFromJsonAsync<ChoreDefinitionResponse>())!;

        var assignmentRequestId = Guid.NewGuid();
        var assignmentResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-assignments",
            new CreateChoreAssignmentRequest(assignmentRequestId, definition.Id, "assigned", child.Id,
                new DateOnly(2026, 8, 23), new TimeOnly(18, 0)));
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        var assignment = (await assignmentResponse.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;

        var assignmentKeyReuse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-assignments",
            new CreateChoreAssignmentRequest(assignmentRequestId, definition.Id, "assigned", child.Id,
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
        var reviewPath = $"/api/households/{household.Id}/chore-completions/{second.Id}/review";
        var concurrentReviews = await Task.WhenAll(
            client.PostAsJsonAsync(reviewPath, new ReviewChoreCompletionRequest(second.Version, "approved", null)),
            client.PostAsJsonAsync(reviewPath, new ReviewChoreCompletionRequest(second.Version, "approved", null)));
        Assert.Contains(concurrentReviews, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(concurrentReviews, response => response.StatusCode == HttpStatusCode.Conflict);
        var approvedResponse = concurrentReviews.Single(response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);
        var approved = (await approvedResponse.Content.ReadFromJsonAsync<ChoreCompletionResponse>())!;
        Assert.Equal(15, approved.PointValue);
        Assert.Equal(15, approved.Award?.Amount);

        var approvalReplay = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-completions/{second.Id}/review",
            new ReviewChoreCompletionRequest(second.Version, "approved", null));
        Assert.Equal(HttpStatusCode.OK, approvalReplay.StatusCode);
        Assert.Equal(approved.Award?.TransactionId,
            (await approvalReplay.Content.ReadFromJsonAsync<ChoreCompletionResponse>())!.Award?.TransactionId);

        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(2, await database.DbContext.ChoreCompletions.CountAsync());
        Assert.Equal("Completed", await database.DbContext.ChoreAssignments
            .Where(item => item.Id == assignment.Id).Select(item => item.Status.ToString()).SingleAsync());
        var award = await database.DbContext.PointTransactions.SingleAsync();
        Assert.Equal(15, award.Amount);
        Assert.Equal(child.Id, award.HouseholdMemberId);
        Assert.Equal(second.Id, award.ChoreCompletionId);
    }

    [PostgreSqlFact]
    public async Task ApprovingZeroPointChoreDoesNotCreateLedgerTransaction()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Zero Point Adult", PrimaryEmail = "zero-points@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var client = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(client);
        var childResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Morgan", "mint"));
        var child = (await childResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;
        var definitionResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-definitions",
            new CreateChoreDefinitionRequest(Guid.NewGuid(), "Put shoes away", null, 0));
        var definition = (await definitionResponse.Content.ReadFromJsonAsync<ChoreDefinitionResponse>())!;
        var assignmentResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-assignments",
            new CreateChoreAssignmentRequest(Guid.NewGuid(), definition.Id, "assigned", child.Id,
                new DateOnly(2026, 8, 25), new TimeOnly(18, 0)));
        var assignment = (await assignmentResponse.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;
        var completionResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-assignments/{assignment.Id}/completions",
            new CompleteChoreRequest(Guid.NewGuid(), assignment.Version, child.Id));
        var completion = (await completionResponse.Content.ReadFromJsonAsync<ChoreCompletionResponse>())!;

        var approvalResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-completions/{completion.Id}/review",
            new ReviewChoreCompletionRequest(completion.Version, "approved", null));

        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);
        var approved = (await approvalResponse.Content.ReadFromJsonAsync<ChoreCompletionResponse>())!;
        Assert.Equal(0, approved.PointValue);
        Assert.Null(approved.Award);
        Assert.Empty(await database.DbContext.PointTransactions.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task RecurringScheduleGeneratesOneRetrySafeSnapshottedAssignment()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Schedule Adult", PrimaryEmail = "schedule@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var client = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(client);
        var childResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Taylor", "mint"));
        var child = (await childResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;
        var definitionResponse = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-definitions",
            new CreateChoreDefinitionRequest(Guid.NewGuid(), "Feed Milo", "Before breakfast", 8));
        var definition = (await definitionResponse.Content.ReadFromJsonAsync<ChoreDefinitionResponse>())!;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).Date);
        var requestId = Guid.NewGuid();
        var create = new CreateChoreScheduleRequest(requestId, definition.Id, "assigned", child.Id,
            new ChoreRecurrenceRequest("daily", 1, []), localToday, localToday, null);
        var response = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-schedules", create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var schedule = (await response.Content.ReadFromJsonAsync<ChoreScheduleResponse>())!;

        var replay = await client.PostAsJsonAsync($"/api/households/{household.Id}/chore-schedules", create);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(schedule.Id, (await replay.Content.ReadFromJsonAsync<ChoreScheduleResponse>())!.Id);

        await using (var scope = database.Factory.Services.CreateAsyncScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<ChoreAssignmentGenerator>();
            var first = await generator.GenerateAsync(CancellationToken.None);
            var second = await generator.GenerateAsync(CancellationToken.None);
            Assert.Equal(1, first.AssignmentsGenerated);
            Assert.Equal(0, second.AssignmentsGenerated);
        }

        database.DbContext.ChangeTracker.Clear();
        var assignment = await database.DbContext.ChoreAssignments.SingleAsync(item => item.ChoreScheduleId == schedule.Id);
        Assert.Equal("Feed Milo", assignment.TitleSnapshot);
        Assert.Equal(localToday, assignment.ScheduleOccurrenceLocalDate);
        Assert.Equal(ChoreDueTimeResolution.Exact, assignment.DueTimeResolution);
        Assert.Equal(8, assignment.PointValueSnapshot);
        Assert.Empty(database.DbContext.PointTransactions);
    }

    [PostgreSqlFact]
    public async Task OpenScheduleGeneratesAnUnassignedChoreAndOnlyOneMemberCanClaimIt()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Open Chore Adult", PrimaryEmail = "open-chores@example.test" };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        using var setupClient = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(setupClient);
        var firstChild = (await (await setupClient.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Avery", "mint"))).Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;
        var secondChild = (await (await setupClient.PostAsJsonAsync($"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("Jordan", "coral"))).Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;
        var definition = (await (await setupClient.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-definitions",
            new CreateChoreDefinitionRequest(Guid.NewGuid(), "Unload dishwasher", null, 12)))
            .Content.ReadFromJsonAsync<ChoreDefinitionResponse>())!;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).Date);
        var scheduleResponse = await setupClient.PostAsJsonAsync(
            $"/api/households/{household.Id}/chore-schedules",
            new CreateChoreScheduleRequest(Guid.NewGuid(), definition.Id, "open", null,
                new ChoreRecurrenceRequest("daily", 1, []), localToday, localToday, null));
        Assert.Equal(HttpStatusCode.Created, scheduleResponse.StatusCode);
        var schedule = (await scheduleResponse.Content.ReadFromJsonAsync<ChoreScheduleResponse>())!;
        Assert.Equal("open", schedule.AssignmentMode);
        Assert.Null(schedule.AssignedMember);

        await using (var scope = database.Factory.Services.CreateAsyncScope())
        {
            var generated = await scope.ServiceProvider.GetRequiredService<ChoreAssignmentGenerator>()
                .GenerateAsync(CancellationToken.None);
            Assert.Equal(1, generated.AssignmentsGenerated);
        }

        database.DbContext.ChangeTracker.Clear();
        var assignment = await database.DbContext.ChoreAssignments.AsNoTracking()
            .SingleAsync(item => item.ChoreScheduleId == schedule.Id);
        Assert.Equal(ChoreAssignmentMode.Open, assignment.AssignmentMode);
        Assert.Null(assignment.HouseholdMemberId);

        var now = DateTimeOffset.UtcNow;
        var session = new UserSession
        {
            UserAccountId = account.Id,
            SelectedHouseholdId = household.Id,
            IsSharedDisplay = true,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(2),
        };
        database.DbContext.UserSessions.Add(session);
        await database.DbContext.SaveChangesAsync();
        using var firstClient = Client(database.Factory, account.Id, session.Id);
        using var secondClient = Client(database.Factory, account.Id, session.Id);
        var firstRequestId = Guid.NewGuid();
        var claims = await Task.WhenAll(
            firstClient.PostAsJsonAsync($"/api/households/{household.Id}/chore-assignments/{assignment.Id}/claim",
                new ClaimChoreAssignmentRequest(firstRequestId, assignment.Version, firstChild.Id)),
            secondClient.PostAsJsonAsync($"/api/households/{household.Id}/chore-assignments/{assignment.Id}/claim",
                new ClaimChoreAssignmentRequest(Guid.NewGuid(), assignment.Version, secondChild.Id)));
        Assert.Single(claims, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(claims, response => response.StatusCode == HttpStatusCode.Conflict);

        var claimed = (await claims.Single(response => response.StatusCode == HttpStatusCode.OK)
            .Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;
        Assert.NotNull(claimed.AssignedMember);
        Assert.NotNull(claimed.ClaimedAt);
        Assert.Equal("open", claimed.AssignmentMode);
        database.DbContext.ChangeTracker.Clear();
        var stored = await database.DbContext.ChoreAssignments.AsNoTracking().SingleAsync(item => item.Id == assignment.Id);
        Assert.Equal(claimed.AssignedMember.Id, stored.HouseholdMemberId);
        Assert.True(stored.ClaimedFromSharedDisplay);
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
