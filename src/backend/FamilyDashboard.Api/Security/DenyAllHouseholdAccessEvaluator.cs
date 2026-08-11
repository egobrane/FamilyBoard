namespace FamilyDashboard.Api.Security;

public sealed class DenyAllHouseholdAccessEvaluator : IHouseholdAccessEvaluator
{
    public ValueTask<HouseholdAccessLevel> GetAccessAsync(
        Guid userAccountId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(HouseholdAccessLevel.None);
    }
}
