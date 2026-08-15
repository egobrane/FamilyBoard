using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Features.Invitations;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class InvitationEndpointTests
{
    [PostgreSqlFact]
    public async Task CreatePrepareAndAcceptCreatesOneAdultMembershipAndSelectsHousehold()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var owner = await AddAccountAsync(database, "Owner Adult", "owner@example.test");
        using var ownerClient = Client(database, owner.Id);
        var household = await BootstrapAsync(ownerClient, "Invitation Household");

        using var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/households/{household.Id}/invitations",
            new CreateInvitationRequest("  INVITED@Example.Test "));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedInvitationResponse>();
        Assert.NotNull(created);
        Assert.Equal("invited@example.test", created.Invitation.IntendedEmail);
        Assert.Equal(43, created.Token.Length);

        database.DbContext.ChangeTracker.Clear();
        var stored = await database.DbContext.HouseholdInvitations.SingleAsync();
        Assert.Equal(32, stored.TokenHash.Length);
        Assert.DoesNotContain(created.Token, Convert.ToHexString(stored.TokenHash));

        var invited = await AddAccountAsync(database, "Invited Adult", "invited@example.test");
        var session = await AddSessionAsync(database, invited);
        using var invitedClient = Client(database, invited.Id, session.Id);
        invitedClient.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        using var prepareResponse = await invitedClient.PostAsJsonAsync(
            "/api/invitations/prepare",
            new PrepareInvitationRequest(created.Token));
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        var cookie = prepareResponse.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith(PendingInvitationCookieService.CookieName, StringComparison.Ordinal));
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(created.Token, cookie);
        invitedClient.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);

        using var acceptResponse = await invitedClient.PostAsync(
            "/api/invitations/pending/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var accepted = await acceptResponse.Content.ReadFromJsonAsync<AcceptedInvitationResponse>();
        Assert.NotNull(accepted);
        Assert.Equal(household.Id, accepted.SelectedHouseholdId);
        Assert.False(accepted.ReusedExistingMembership);

        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(2, await database.DbContext.HouseholdMemberships.CountAsync(candidate =>
            candidate.HouseholdId == household.Id));
        Assert.Equal(2, await database.DbContext.HouseholdMembers.CountAsync(candidate =>
            candidate.HouseholdId == household.Id && candidate.Role == HouseholdMemberRole.Adult));
        Assert.Equal(
            HouseholdInvitationStatus.Accepted,
            await database.DbContext.HouseholdInvitations.Select(candidate => candidate.Status).SingleAsync());
        Assert.Equal(
            household.Id,
            await database.DbContext.UserSessions.Where(candidate => candidate.Id == session.Id)
                .Select(candidate => candidate.SelectedHouseholdId).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task CrossHouseholdAdultsCannotListOrRevokeInvitations()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var first = await AddAccountAsync(database, "First", "first@example.test");
        var second = await AddAccountAsync(database, "Second", "second@example.test");
        using var firstClient = Client(database, first.Id);
        using var secondClient = Client(database, second.Id);
        var firstHousehold = await BootstrapAsync(firstClient, "First Household");
        await BootstrapAsync(secondClient, "Second Household");
        var created = await (await firstClient.PostAsJsonAsync(
            $"/api/households/{firstHousehold.Id}/invitations",
            new CreateInvitationRequest("invite@example.test")))
            .Content.ReadFromJsonAsync<CreatedInvitationResponse>();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await secondClient.GetAsync($"/api/households/{firstHousehold.Id}/invitations")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await secondClient.PostAsync(
                $"/api/households/{firstHousehold.Id}/invitations/{created!.Invitation.Id}/revoke",
                null)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task AcceptanceRejectsWrongEmailAndLeavesInvitationPending()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var owner = await AddAccountAsync(database, "Owner", "owner2@example.test");
        using var ownerClient = Client(database, owner.Id);
        var household = await BootstrapAsync(ownerClient, "Email Bound Household");
        var created = await (await ownerClient.PostAsJsonAsync(
            $"/api/households/{household.Id}/invitations",
            new CreateInvitationRequest("right@example.test")))
            .Content.ReadFromJsonAsync<CreatedInvitationResponse>();
        var wrong = await AddAccountAsync(database, "Wrong", "wrong@example.test");
        var session = await AddSessionAsync(database, wrong);
        using var client = Client(database, wrong.Id, session.Id);
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        var prepare = await client.PostAsJsonAsync(
            "/api/invitations/prepare", new PrepareInvitationRequest(created!.Token));
        client.DefaultRequestHeaders.Add(
            "Cookie",
            prepare.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);

        using var response = await client.PostAsync("/api/invitations/pending/accept", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ApiProblemCodes.InvitationEmailMismatch, await ProblemCodeAsync(response));
        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            HouseholdInvitationStatus.Pending,
            await database.DbContext.HouseholdInvitations.Select(candidate => candidate.Status).SingleAsync());
        Assert.Equal(1, await database.DbContext.HouseholdMemberships.CountAsync());
    }

    [PostgreSqlFact]
    public async Task PrepareRequiresConfiguredOriginAndJson()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var client = database.Factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/invitations/prepare", new PrepareInvitationRequest("irrelevant"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ApiProblemCodes.InvitationOriginNotAllowed, await ProblemCodeAsync(response));
    }

    [PostgreSqlFact]
    public async Task ConcurrentAcceptanceBySameAccountIsIdempotent()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var owner = await AddAccountAsync(database, "Owner", "owner3@example.test");
        using var ownerClient = Client(database, owner.Id);
        var household = await BootstrapAsync(ownerClient, "Concurrent Household");
        var created = await (await ownerClient.PostAsJsonAsync(
            $"/api/households/{household.Id}/invitations",
            new CreateInvitationRequest("concurrent@example.test")))
            .Content.ReadFromJsonAsync<CreatedInvitationResponse>();
        var invited = await AddAccountAsync(database, "Concurrent Adult", "concurrent@example.test");
        var firstSession = await AddSessionAsync(database, invited);
        var secondSession = await AddSessionAsync(database, invited);
        using var firstClient = Client(database, invited.Id, firstSession.Id);
        using var secondClient = Client(database, invited.Id, secondSession.Id);
        await AddPendingCookieAsync(firstClient, created!.Token);
        await AddPendingCookieAsync(secondClient, created.Token);

        var responses = await Task.WhenAll(
            firstClient.PostAsync("/api/invitations/pending/accept", null),
            secondClient.PostAsync("/api/invitations/pending/accept", null));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, await database.DbContext.HouseholdMemberships.CountAsync(candidate =>
            candidate.HouseholdId == household.Id && candidate.UserAccountId == invited.Id));
        Assert.Equal(2, await database.DbContext.UserSessions.CountAsync(candidate =>
            candidate.UserAccountId == invited.Id && candidate.SelectedHouseholdId == household.Id));
    }

    [PostgreSqlFact]
    public async Task RevokedInvitationCannotBePreparedAndRevocationIsIdempotent()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var owner = await AddAccountAsync(database, "Owner", "owner4@example.test");
        using var ownerClient = Client(database, owner.Id);
        var household = await BootstrapAsync(ownerClient, "Revocation Household");
        var created = await (await ownerClient.PostAsJsonAsync(
            $"/api/households/{household.Id}/invitations",
            new CreateInvitationRequest("revoked@example.test")))
            .Content.ReadFromJsonAsync<CreatedInvitationResponse>();
        var path = $"/api/households/{household.Id}/invitations/{created!.Invitation.Id}/revoke";

        Assert.Equal(HttpStatusCode.OK, (await ownerClient.PostAsync(path, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.PostAsync(path, null)).StatusCode);

        using var anonymous = database.Factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        using var response = await anonymous.PostAsJsonAsync(
            "/api/invitations/prepare", new PrepareInvitationRequest(created.Token));
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(ApiProblemCodes.InvitationRevoked, await ProblemCodeAsync(response));
    }

    [PostgreSqlFact]
    public async Task ExpiredPendingInvitationIsClosedBeforeReplacementIsCreated()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var owner = await AddAccountAsync(database, "Owner", "owner5@example.test");
        using var client = Client(database, owner.Id);
        var household = await BootstrapAsync(client, "Replacement Household");
        var first = await (await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/invitations",
            new CreateInvitationRequest("replace@example.test")))
            .Content.ReadFromJsonAsync<CreatedInvitationResponse>();
        database.DbContext.ChangeTracker.Clear();
        var invitation = await database.DbContext.HouseholdInvitations
            .SingleAsync(candidate => candidate.Id == first!.Invitation.Id);
        invitation.CreatedAt = DateTimeOffset.UtcNow.AddDays(-9);
        invitation.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-2);
        await database.DbContext.SaveChangesAsync();

        using var response = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/invitations",
            new CreateInvitationRequest("replace@example.test"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, await database.DbContext.HouseholdInvitations.CountAsync(candidate =>
            candidate.Status == HouseholdInvitationStatus.Expired));
        Assert.Equal(1, await database.DbContext.HouseholdInvitations.CountAsync(candidate =>
            candidate.Status == HouseholdInvitationStatus.Pending));
    }

    private static async Task<UserAccount> AddAccountAsync(
        PostgreSqlTestDatabase database, string name, string email)
    {
        var account = new UserAccount { DisplayName = name, PrimaryEmail = email };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        return account;
    }

    private static async Task<UserSession> AddSessionAsync(
        PostgreSqlTestDatabase database, UserAccount account)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new UserSession
        {
            UserAccountId = account.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(2),
        };
        database.DbContext.UserSessions.Add(session);
        await database.DbContext.SaveChangesAsync();
        return session;
    }

    private static HttpClient Client(
        PostgreSqlTestDatabase database, Guid accountId, Guid? sessionId = null)
    {
        var client = database.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, accountId.ToString());
        if (sessionId is not null)
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SessionIdHeaderName, sessionId.ToString());
        return client;
    }

    private static async Task AddPendingCookieAsync(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        var response = await client.PostAsJsonAsync(
            "/api/invitations/prepare", new PrepareInvitationRequest(token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Add(
            "Cookie",
            response.Headers.GetValues("Set-Cookie").Single(value =>
                value.StartsWith(PendingInvitationCookieService.CookieName, StringComparison.Ordinal))
                .Split(';')[0]);
    }

    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/households",
            new CreateHouseholdRequest(name, "America/New_York", "en-US", "Sunday"));
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }
}
