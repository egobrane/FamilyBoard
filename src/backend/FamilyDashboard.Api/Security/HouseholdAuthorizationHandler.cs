using Microsoft.AspNetCore.Authorization;

namespace FamilyDashboard.Api.Security;

public sealed class HouseholdAuthorizationHandler(IHouseholdAccessEvaluator accessEvaluator)
    : AuthorizationHandler<HouseholdAccessRequirement, HouseholdAccessResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HouseholdAccessRequirement requirement,
        HouseholdAccessResource resource)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userAccountIdValue = context.User.FindFirst(FamilyDashboardClaimTypes.UserAccountId)?.Value;
        if (!Guid.TryParse(userAccountIdValue, out var userAccountId))
        {
            return;
        }

        var access = await accessEvaluator.GetAccessAsync(
            userAccountId,
            resource.HouseholdId,
            resource.CancellationToken);

        if (access >= requirement.MinimumAccess)
        {
            context.Succeed(requirement);
        }
    }
}
