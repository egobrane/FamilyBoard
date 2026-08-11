using Microsoft.AspNetCore.Authorization;

namespace FamilyDashboard.Api.Security;

public enum HouseholdAccessLevel
{
    None = 0,
    Member = 1,
    Adult = 2,
}

public sealed record HouseholdAccessResource(
    Guid HouseholdId,
    CancellationToken CancellationToken = default);

public sealed record HouseholdAccessRequirement(HouseholdAccessLevel MinimumAccess)
    : IAuthorizationRequirement;

public static class HouseholdAuthorizationPolicies
{
    public const string Member = "HouseholdMember";
    public const string Adult = "HouseholdAdult";
}
