using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Persistence;

[Collection("PostgreSQL integration")]
public sealed class UserSessionPersistenceTests
{
    [PostgreSqlFact]
    public async Task SessionRenewsWithinAbsoluteLimitAndRevokesImmediately()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var account = new UserAccount
        {
            DisplayName = "Alex Adult",
            PrimaryEmail = "alex@example.test",
        };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        var service = CreateService(database.DbContext, clock);

        var session = await service.CreateAsync(account, false, "Kitchen display", default);
        Assert.Equal(now.AddDays(14), session.ExpiresAt);
        Assert.Equal(now.AddDays(30), session.AbsoluteExpiresAt);
        Assert.False(session.IsSharedDisplay);

        clock.Advance(TimeSpan.FromDays(13));
        var renewed = await service.ValidateAndRenewAsync(session.Id, account.Id, default);
        Assert.True(renewed.IsValid);
        Assert.True(renewed.WasRenewed);
        Assert.Equal(now.AddDays(27), renewed.Session!.ExpiresAt);

        await service.RevokeCurrentAsync(UserSessionService.CreatePrincipal(session), default);
        var revoked = await service.ValidateAndRenewAsync(session.Id, account.Id, default);
        Assert.False(revoked.IsValid);
    }

    [PostgreSqlFact]
    public async Task SessionConstraintsRejectInvalidExpirationOrdering()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount
        {
            DisplayName = "Alex Adult",
            PrimaryEmail = "alex@example.test",
        };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        database.DbContext.UserSessions.Add(new UserSession
        {
            UserAccountId = account.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now,
            AbsoluteExpiresAt = now.AddDays(1),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.DbContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task SessionCannotSelectAHouseholdWithoutAnAccountMembership()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount
        {
            DisplayName = "First Adult",
            PrimaryEmail = "first@example.test",
        };
        var otherAccount = new UserAccount
        {
            DisplayName = "Other Adult",
            PrimaryEmail = "other@example.test",
        };
        var household = new Household { Name = "Other Household" };
        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            DisplayName = otherAccount.DisplayName,
            Role = HouseholdMemberRole.Adult,
        };
        household.Configuration = new HouseholdConfiguration { HouseholdId = household.Id };
        household.Members.Add(member);
        household.Memberships.Add(new HouseholdMembership
        {
            UserAccountId = otherAccount.Id,
            HouseholdId = household.Id,
            HouseholdMemberId = member.Id,
        });
        database.DbContext.AddRange(account, otherAccount, household);
        await database.DbContext.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        database.DbContext.UserSessions.Add(new UserSession
        {
            UserAccountId = account.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(2),
            SelectedHouseholdId = household.Id,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.DbContext.SaveChangesAsync());
    }

    private static UserSessionService CreateService(
        FamilyDashboard.Api.Persistence.FamilyDashboardDbContext context,
        TimeProvider clock) =>
        new(context, clock, Options.Create(new AuthenticationConfiguration()));

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset _value = value;

        public override DateTimeOffset GetUtcNow() => _value;

        public void Advance(TimeSpan duration) => _value = _value.Add(duration);
    }
}
