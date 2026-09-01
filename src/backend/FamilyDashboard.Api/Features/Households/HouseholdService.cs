using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using FamilyDashboard.Api.Features.HouseholdMembers;

namespace FamilyDashboard.Api.Features.Households;

internal sealed class HouseholdService(FamilyDashboardDbContext dbContext)
{
    public Task<UserAccount?> FindActiveAccountAsync(
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        return dbContext.UserAccounts
            .SingleOrDefaultAsync(
                account => account.Id == userAccountId && account.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<HouseholdSummaryResponse>> ListAsync(
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        var memberships = await dbContext.HouseholdMemberships
            .AsNoTracking()
            .Include(membership => membership.Household)
            .Include(membership => membership.HouseholdMember).ThenInclude(member => member.CurrentPhotoAsset)
            .Where(membership =>
                membership.UserAccountId == userAccountId
                && membership.UserAccount.IsActive
                && membership.Household.IsActive
                && membership.HouseholdMember.IsActive)
            .OrderBy(membership => membership.Household.Name)
            .ThenBy(membership => membership.HouseholdId)
            .ToListAsync(cancellationToken);
        return memberships.Select(membership => new HouseholdSummaryResponse(
                membership.HouseholdId,
                membership.Household.Name,
                membership.HouseholdMemberId,
                HouseholdContractRoles.FromDomain(membership.HouseholdMember.Role),
                membership.HouseholdMember.AvatarColor,
                HouseholdMemberPhotoContracts.Map(membership.HouseholdMember))).ToArray();
    }

    public async Task<HouseholdResponse> CreateAsync(
        UserAccount account,
        ValidatedHouseholdValues values,
        UserSession? currentSession,
        CancellationToken cancellationToken)
    {
        var household = new Household
        {
            Name = values.Name,
        };
        var configuration = new HouseholdConfiguration
        {
            HouseholdId = household.Id,
            TimeZone = values.TimeZone,
            Locale = values.Locale,
            WeekStartsOn = values.WeekStartsOn,
        };
        var adultMember = new HouseholdMember
        {
            HouseholdId = household.Id,
            DisplayName = account.DisplayName,
            Role = HouseholdMemberRole.Adult,
        };
        var membership = new HouseholdMembership
        {
            UserAccountId = account.Id,
            HouseholdId = household.Id,
            HouseholdMemberId = adultMember.Id,
        };

        household.Configuration = configuration;
        household.Members.Add(adultMember);
        household.Memberships.Add(membership);
        account.HouseholdMemberships.Add(membership);
        adultMember.Membership = membership;
        if (currentSession is not null)
        {
            currentSession.SelectedHouseholdId = household.Id;
            currentSession.SelectedHouseholdMembership = membership;
        }

        dbContext.Households.Add(household);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(household, configuration, adultMember);
    }

    public async Task<HouseholdResponse?> GetAsync(
        Guid householdId,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        return await dbContext.HouseholdMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.HouseholdId == householdId
                && membership.UserAccountId == userAccountId
                && membership.UserAccount.IsActive
                && membership.Household.IsActive
                && membership.HouseholdMember.IsActive)
            .Select(membership => new HouseholdResponse(
                membership.HouseholdId,
                membership.Household.Name,
                membership.Household.Configuration!.TimeZone,
                membership.Household.Configuration.Locale,
                membership.Household.Configuration.WeekStartsOn.ToString().ToLowerInvariant(),
                new HouseholdAccessResponse(
                    membership.HouseholdMemberId,
                    HouseholdContractRoles.FromDomain(membership.HouseholdMember.Role),
                    membership.HouseholdMember.Role == HouseholdMemberRole.Adult)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<HouseholdResponse?> UpdateAsync(
        Guid householdId,
        Guid userAccountId,
        ValidatedHouseholdPatch patch,
        CancellationToken cancellationToken)
    {
        var household = await dbContext.Households
            .Include(candidate => candidate.Configuration)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == householdId && candidate.IsActive,
                cancellationToken);
        var membership = await dbContext.HouseholdMemberships
            .Include(candidate => candidate.HouseholdMember)
            .SingleOrDefaultAsync(
                candidate => candidate.HouseholdId == householdId
                    && candidate.UserAccountId == userAccountId,
                cancellationToken);

        if (household?.Configuration is null || membership?.HouseholdMember is null)
        {
            return null;
        }

        if (patch.Name is not null)
        {
            household.Name = patch.Name;
        }

        if (patch.TimeZone is not null)
        {
            household.Configuration.TimeZone = patch.TimeZone;
        }

        if (patch.Locale is not null)
        {
            household.Configuration.Locale = patch.Locale;
        }

        if (patch.WeekStartsOn is not null)
        {
            household.Configuration.WeekStartsOn = patch.WeekStartsOn.Value;
        }

        var now = DateTimeOffset.UtcNow;
        household.UpdatedAt = now;
        household.Configuration.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(household, household.Configuration, membership.HouseholdMember);
    }

    private static HouseholdResponse Map(
        Household household,
        HouseholdConfiguration configuration,
        HouseholdMember member)
    {
        return new HouseholdResponse(
            household.Id,
            household.Name,
            configuration.TimeZone,
            configuration.Locale,
            configuration.WeekStartsOn.ToString().ToLowerInvariant(),
            new HouseholdAccessResponse(
                member.Id,
                HouseholdContractRoles.FromDomain(member.Role),
                member.Role == HouseholdMemberRole.Adult));
    }
}
