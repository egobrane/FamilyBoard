using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Features.Calendar;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Persistence;

[Collection("PostgreSQL integration")]
public sealed class CalendarPersistenceTests
{
    [PostgreSqlFact]
    public async Task OneConnectionPerAdultAndOneSourceMappingAreEnforced()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "Calendar Adult", PrimaryEmail = "calendar@example.test" };
        var household = new Household { Name = "Calendar Family" };
        household.Configuration = new HouseholdConfiguration { HouseholdId = household.Id };
        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            DisplayName = account.DisplayName,
            Role = HouseholdMemberRole.Adult,
        };
        var membership = new HouseholdMembership
        {
            UserAccountId = account.Id,
            HouseholdId = household.Id,
            HouseholdMemberId = member.Id,
        };
        household.Members.Add(member);
        household.Memberships.Add(membership);
        account.HouseholdMemberships.Add(membership);
        member.Membership = membership;
        var connection = Connection(account.Id, "subject-one");
        database.DbContext.AddRange(account, household, connection);
        await database.DbContext.SaveChangesAsync();
        database.DbContext.HouseholdCalendarSources.Add(new HouseholdCalendarSource
        {
            HouseholdId = household.Id,
            GoogleCalendarConnectionId = connection.Id,
            OwnerUserAccountId = account.Id,
            ExternalCalendarId = "primary@example.test",
            DisplayNameSnapshot = "Family",
            AddedByUserAccountId = account.Id,
        });
        await database.DbContext.SaveChangesAsync();

        database.DbContext.ChangeTracker.Clear();
        database.DbContext.GoogleCalendarConnections.Add(Connection(account.Id, "subject-two"));
        await Assert.ThrowsAsync<DbUpdateException>(() => database.DbContext.SaveChangesAsync());
        database.DbContext.ChangeTracker.Clear();

        database.DbContext.HouseholdCalendarSources.Add(new HouseholdCalendarSource
        {
            HouseholdId = household.Id,
            GoogleCalendarConnectionId = connection.Id,
            OwnerUserAccountId = account.Id,
            ExternalCalendarId = "primary@example.test",
            DisplayNameSnapshot = "Duplicate",
            AddedByUserAccountId = account.Id,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => database.DbContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task ActiveConnectionRequiresProtectedRefreshToken()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount { DisplayName = "No Token", PrimaryEmail = "no-token@example.test" };
        database.DbContext.Add(account);
        await database.DbContext.SaveChangesAsync();
        database.DbContext.GoogleCalendarConnections.Add(new GoogleCalendarConnection
        {
            UserAccountId = account.Id,
            ProviderSubject = "subject",
            ProviderEmailNormalized = account.PrimaryEmail,
            GrantedScopes = GoogleCalendarScopes.EventsReadOnly,
            Status = GoogleCalendarConnectionStatus.Active,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.DbContext.SaveChangesAsync());
    }

    private static GoogleCalendarConnection Connection(Guid userAccountId, string subject) => new()
    {
        UserAccountId = userAccountId,
        ProviderSubject = subject,
        ProviderEmailNormalized = "calendar@example.test",
        ProtectedRefreshToken = "protected",
        GrantedScopes = GoogleCalendarScopes.EventsReadOnly,
        Status = GoogleCalendarConnectionStatus.Active,
    };
}
