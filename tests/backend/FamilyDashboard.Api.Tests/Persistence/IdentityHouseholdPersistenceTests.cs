using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Persistence;

[Collection("PostgreSQL integration")]
public sealed class IdentityHouseholdPersistenceTests
{
    [PostgreSqlFact]
    public async Task MembershipCompositeForeignKeyRejectsProfileFromAnotherHousehold()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = new UserAccount
        {
            DisplayName = "Adult",
            PrimaryEmail = "constraint@example.test",
        };
        var firstHousehold = CreateHousehold("First");
        var secondHousehold = CreateHousehold("Second");
        var secondMember = new HouseholdMember
        {
            HouseholdId = secondHousehold.Id,
            DisplayName = "Adult",
            Role = HouseholdMemberRole.Adult,
        };
        database.DbContext.AddRange(account, firstHousehold, secondHousehold, secondMember);
        await database.DbContext.SaveChangesAsync();
        database.DbContext.HouseholdMemberships.Add(new HouseholdMembership
        {
            UserAccountId = account.Id,
            HouseholdId = firstHousehold.Id,
            HouseholdMemberId = secondMember.Id,
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.DbContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task ProviderAndSubjectCombinationIsUnique()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var firstAccount = new UserAccount
        {
            DisplayName = "First",
            PrimaryEmail = "first-identity@example.test",
        };
        var secondAccount = new UserAccount
        {
            DisplayName = "Second",
            PrimaryEmail = "second-identity@example.test",
        };
        database.DbContext.AddRange(firstAccount, secondAccount);
        database.DbContext.ExternalIdentities.AddRange(
            new ExternalIdentity
            {
                UserAccountId = firstAccount.Id,
                Provider = "google",
                ProviderSubject = "shared-subject",
            },
            new ExternalIdentity
            {
                UserAccountId = secondAccount.Id,
                Provider = "google",
                ProviderSubject = "shared-subject",
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.DbContext.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task ConcurrentAdultDeactivationAlwaysLeavesAnActiveAdult()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var household = CreateHousehold("Concurrent Household");
        var firstAccount = new UserAccount
        {
            DisplayName = "First Adult",
            PrimaryEmail = "first-concurrent@example.test",
        };
        var secondAccount = new UserAccount
        {
            DisplayName = "Second Adult",
            PrimaryEmail = "second-concurrent@example.test",
        };
        var firstMember = CreateAdultMember(household, firstAccount);
        var secondMember = CreateAdultMember(household, secondAccount);
        database.DbContext.AddRange(household, firstAccount, secondAccount);
        await database.DbContext.SaveChangesAsync();

        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!;
        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var firstContext = new FamilyDashboardDbContext(options);
        await using var secondContext = new FamilyDashboardDbContext(options);
        var firstService = new HouseholdMemberService(firstContext);
        var secondService = new HouseholdMemberService(secondContext);
        var patch = new ValidatedHouseholdMemberPatch(null, null, false);

        var results = await Task.WhenAll(
            firstService.UpdateAsync(
                household.Id,
                firstMember.Id,
                secondAccount.Id,
                patch,
                CancellationToken.None),
            secondService.UpdateAsync(
                household.Id,
                secondMember.Id,
                firstAccount.Id,
                patch,
                CancellationToken.None));

        Assert.Contains(results, result => result.Status == HouseholdMemberUpdateStatus.Success);
        Assert.Contains(results, result =>
            result.Status is HouseholdMemberUpdateStatus.LastActiveAdult
                or HouseholdMemberUpdateStatus.Conflict);
        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, await database.DbContext.HouseholdMembers.CountAsync(
            member => member.HouseholdId == household.Id
                && member.IsActive
                && member.Role == HouseholdMemberRole.Adult));
    }

    [PostgreSqlFact]
    public async Task ServiceRejectsDeactivationOfTheLastActiveAdult()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var household = CreateHousehold("Last Adult Household");
        var account = new UserAccount
        {
            DisplayName = "Only Adult",
            PrimaryEmail = "only-persistence@example.test",
        };
        var member = CreateAdultMember(household, account);
        database.DbContext.AddRange(household, account);
        await database.DbContext.SaveChangesAsync();
        var service = new HouseholdMemberService(database.DbContext);

        var result = await service.UpdateAsync(
            household.Id,
            member.Id,
            Guid.NewGuid(),
            new ValidatedHouseholdMemberPatch(null, null, false),
            CancellationToken.None);

        Assert.Equal(HouseholdMemberUpdateStatus.LastActiveAdult, result.Status);
        database.DbContext.ChangeTracker.Clear();
        Assert.True(await database.DbContext.HouseholdMembers
            .Where(candidate => candidate.Id == member.Id)
            .Select(candidate => candidate.IsActive)
            .SingleAsync());
    }

    private static Household CreateHousehold(string name)
    {
        var household = new Household { Name = name };
        household.Configuration = new HouseholdConfiguration { HouseholdId = household.Id };
        return household;
    }

    private static HouseholdMember CreateAdultMember(
        Household household,
        UserAccount account)
    {
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
        return member;
    }
}
