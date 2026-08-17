using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Features.ParentAccess;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class ParentAccessEndpointTests
{
    [PostgreSqlFact]
    public async Task SharedDisplayRequiresHouseholdScopedElevationForAdministration()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (account, session, client) = await CreateActorAsync(database);
        using (client)
        {
            var household = await BootstrapAsync(client, "Access Family");
            using var setup = await client.PutAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/pin",
                new SetParentPinRequest("482913"));
            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

            using var enable = await client.PutAsJsonAsync(
                "/api/auth/session/shared-display",
                new UpdateSharedDisplayRequest(household.Id, true, "Kitchen display"));
            Assert.Equal(HttpStatusCode.OK, enable.StatusCode);

            using var lockedUpdate = await client.PatchAsJsonAsync(
                $"/api/households/{household.Id}",
                new UpdateHouseholdRequest("Blocked", null, null, null));
            Assert.Equal(HttpStatusCode.Forbidden, lockedUpdate.StatusCode);
            Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(lockedUpdate));

            using var verify = await client.PostAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/verify",
                new VerifyParentPinRequest("482913"));
            Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

            using var allowedUpdate = await client.PatchAsJsonAsync(
                $"/api/households/{household.Id}",
                new UpdateHouseholdRequest("Unlocked", null, null, null));
            Assert.Equal(HttpStatusCode.OK, allowedUpdate.StatusCode);

            database.DbContext.ChangeTracker.Clear();
            var persisted = await database.DbContext.UserSessions.SingleAsync(value => value.Id == session.Id);
            Assert.True(persisted.IsSharedDisplay);
            Assert.Equal(household.Id, persisted.AdministrativeElevationHouseholdId);
            Assert.Equal(3, await database.DbContext.ParentAccessAuditEvents.CountAsync());
            Assert.Equal(account.Id, persisted.UserAccountId);
        }
    }

    [PostgreSqlFact]
    public async Task FiveWrongPinsStartSessionCooldownWithoutLeakingPinMaterial()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (_, session, client) = await CreateActorAsync(database);
        using (client)
        {
            var household = await BootstrapAsync(client, "Cooldown Family");
            using var setup = await client.PutAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/pin",
                new SetParentPinRequest("482913"));
            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

            HttpResponseMessage? last = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                last?.Dispose();
                last = await client.PostAsJsonAsync(
                    $"/api/households/{household.Id}/parent-access/verify",
                    new VerifyParentPinRequest("000000"));
            }
            using (last)
            {
                Assert.NotNull(last);
                Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
                Assert.Equal(ApiProblemCodes.ParentPinLocked, await ProblemCodeAsync(last));
                Assert.True(last.Headers.Contains("Retry-After"));
            }

            database.DbContext.ChangeTracker.Clear();
            var persisted = await database.DbContext.UserSessions.SingleAsync(value => value.Id == session.Id);
            Assert.Equal(5, persisted.ParentAccessFailedAttemptCount);
            Assert.NotNull(persisted.ParentAccessLockedUntil);
            var serializedEvents = JsonSerializer.Serialize(
                await database.DbContext.ParentAccessAuditEvents.AsNoTracking().ToListAsync());
            Assert.DoesNotContain("482913", serializedEvents, StringComparison.Ordinal);
            Assert.DoesNotContain("000000", serializedEvents, StringComparison.Ordinal);
        }
    }

    [PostgreSqlFact]
    public async Task SelectingAnotherHouseholdClearsElevation()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (_, session, client) = await CreateActorAsync(database);
        using (client)
        {
            var first = await BootstrapAsync(client, "First Family");
            var second = await BootstrapAsync(client, "Second Family");
            database.DbContext.ChangeTracker.Clear();
            var persisted = await database.DbContext.UserSessions.SingleAsync(value => value.Id == session.Id);
            persisted.AdministrativeElevationHouseholdId = second.Id;
            persisted.AdministrativeElevationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
            await database.DbContext.SaveChangesAsync();

            using var selection = await client.PutAsJsonAsync(
                "/api/auth/session/household", new { householdId = first.Id });
            Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
            database.DbContext.ChangeTracker.Clear();
            persisted = await database.DbContext.UserSessions.SingleAsync(value => value.Id == session.Id);
            Assert.Null(persisted.AdministrativeElevationHouseholdId);
            Assert.Null(persisted.AdministrativeElevationExpiresAt);
        }
    }

    [PostgreSqlFact]
    public async Task ExplicitLockAndExpiredElevationBothFailClosedOnSharedDisplay()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (_, session, client) = await CreateActorAsync(database);
        using (client)
        {
            var household = await BootstrapAsync(client, "Locked Family");
            using var setup = await client.PutAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/pin",
                new SetParentPinRequest("482913"));
            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
            using var enable = await client.PutAsJsonAsync(
                "/api/auth/session/shared-display",
                new UpdateSharedDisplayRequest(household.Id, true, "Hall display"));
            Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
            using var verify = await client.PostAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/verify",
                new VerifyParentPinRequest("482913"));
            Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

            using var lockResponse = await client.PostAsync(
                $"/api/households/{household.Id}/parent-access/lock", null);
            Assert.Equal(HttpStatusCode.NoContent, lockResponse.StatusCode);
            using var lockedUpdate = await client.PatchAsJsonAsync(
                $"/api/households/{household.Id}",
                new UpdateHouseholdRequest("Still locked", null, null, null));
            Assert.Equal(HttpStatusCode.Forbidden, lockedUpdate.StatusCode);

            using var verifyAgain = await client.PostAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/verify",
                new VerifyParentPinRequest("482913"));
            Assert.Equal(HttpStatusCode.OK, verifyAgain.StatusCode);
            database.DbContext.ChangeTracker.Clear();
            var persisted = await database.DbContext.UserSessions.SingleAsync(value => value.Id == session.Id);
            persisted.AdministrativeElevationExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await database.DbContext.SaveChangesAsync();

            using var expiredUpdate = await client.PatchAsJsonAsync(
                $"/api/households/{household.Id}",
                new UpdateHouseholdRequest("Expired", null, null, null));
            Assert.Equal(HttpStatusCode.Forbidden, expiredUpdate.StatusCode);
            Assert.Equal(ApiProblemCodes.ParentElevationRequired, await ProblemCodeAsync(expiredUpdate));
        }
    }

    [PostgreSqlFact]
    public async Task SharedDisplayCannotBootstrapAndPinRecoveryRequiresRecentPrivateSession()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (_, session, client) = await CreateActorAsync(database);
        using (client)
        {
            var household = await BootstrapAsync(client, "Private Actions Family");
            using var setup = await client.PutAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/pin",
                new SetParentPinRequest("482913"));
            Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
            using var enable = await client.PutAsJsonAsync(
                "/api/auth/session/shared-display",
                new UpdateSharedDisplayRequest(household.Id, true, "Kitchen display"));
            Assert.Equal(HttpStatusCode.OK, enable.StatusCode);

            using var bootstrap = await client.PostAsJsonAsync(
                "/api/households",
                new CreateHouseholdRequest("Blocked Family", "America/New_York", "en-US", "Sunday"));
            Assert.Equal(HttpStatusCode.Forbidden, bootstrap.StatusCode);
            Assert.Equal(ApiProblemCodes.PrivateSessionRequired, await ProblemCodeAsync(bootstrap));

            database.DbContext.ChangeTracker.Clear();
            var persisted = await database.DbContext.UserSessions.SingleAsync(value => value.Id == session.Id);
            persisted.IsSharedDisplay = false;
            persisted.CreatedAt = DateTimeOffset.UtcNow.AddHours(-1);
            persisted.AdministrativeElevationHouseholdId = null;
            persisted.AdministrativeElevationExpiresAt = null;
            await database.DbContext.SaveChangesAsync();

            using var recovery = await client.PostAsJsonAsync(
                $"/api/households/{household.Id}/parent-access/pin/recover",
                new SetParentPinRequest("739251"));
            Assert.Equal(HttpStatusCode.Forbidden, recovery.StatusCode);
            Assert.Equal(ApiProblemCodes.RecentAuthenticationRequired, await ProblemCodeAsync(recovery));
        }
    }

    private static async Task<(UserAccount Account, UserSession Session, HttpClient Client)> CreateActorAsync(
        PostgreSqlTestDatabase database)
    {
        var now = DateTimeOffset.UtcNow;
        var account = new UserAccount { DisplayName = "Parent", PrimaryEmail = "parent@example.test" };
        var session = new UserSession
        {
            UserAccount = account,
            UserAccountId = account.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(2),
        };
        database.DbContext.AddRange(account, session);
        await database.DbContext.SaveChangesAsync();
        var client = database.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, account.Id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SessionIdHeaderName, session.Id.ToString());
        return (account, session, client);
    }

    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/households",
            new CreateHouseholdRequest(name, "America/New_York", "en-US", "Sunday"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
