using FamilyDashboard.Api.Security;

namespace FamilyDashboard.Api.Tests.Authorization;

internal sealed class FakeHouseholdAccessEvaluator : IHouseholdAccessEvaluator
{
    private readonly Dictionary<(Guid UserAccountId, Guid HouseholdId), HouseholdAccessLevel> _access = [];

    public void SetAccess(Guid userAccountId, Guid householdId, HouseholdAccessLevel access)
    {
        _access[(userAccountId, householdId)] = access;
    }

    public ValueTask<HouseholdAccessLevel> GetAccessAsync(
        Guid userAccountId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_access.GetValueOrDefault((userAccountId, householdId)));
    }
}
