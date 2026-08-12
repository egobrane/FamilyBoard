using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Security;

public sealed class EfHouseholdAccessEvaluator(FamilyDashboardDbContext dbContext)
    : IHouseholdAccessEvaluator
{
    public async ValueTask<HouseholdAccessLevel> GetAccessAsync(
        Guid userAccountId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var role = await dbContext.HouseholdMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserAccountId == userAccountId
                && membership.HouseholdId == householdId
                && membership.UserAccount.IsActive
                && membership.Household.IsActive
                && membership.HouseholdMember.IsActive)
            .Select(membership => (HouseholdMemberRole?)membership.HouseholdMember.Role)
            .SingleOrDefaultAsync(cancellationToken);

        return role switch
        {
            HouseholdMemberRole.Adult => HouseholdAccessLevel.Adult,
            HouseholdMemberRole.Child => HouseholdAccessLevel.Member,
            _ => HouseholdAccessLevel.None,
        };
    }
}
