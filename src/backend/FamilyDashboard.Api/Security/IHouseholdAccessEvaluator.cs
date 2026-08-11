namespace FamilyDashboard.Api.Security;

public interface IHouseholdAccessEvaluator
{
    ValueTask<HouseholdAccessLevel> GetAccessAsync(
        Guid userAccountId,
        Guid householdId,
        CancellationToken cancellationToken = default);
}
