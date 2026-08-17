using FamilyDashboard.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyDashboard.Api.Security;

public sealed record HouseholdAdministrationRequirement : IAuthorizationRequirement;

public sealed class HouseholdAdministrationAuthorizationHandler(
    IHouseholdAccessEvaluator accessEvaluator,
    IServiceProvider services)
    : AuthorizationHandler<HouseholdAdministrationRequirement, HouseholdAccessResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HouseholdAdministrationRequirement requirement,
        HouseholdAccessResource resource)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId)
            || !context.User.TryGetUserSessionId(out var sessionId))
        {
            return;
        }

        var access = await accessEvaluator.GetAccessAsync(
            userAccountId, resource.HouseholdId, resource.CancellationToken);
        if (access < HouseholdAccessLevel.Adult)
        {
            return;
        }

        var dbContext = services.GetService<FamilyDashboardDbContext>();
        if (dbContext is null)
        {
            return;
        }
        var timeProvider = services.GetService<TimeProvider>() ?? TimeProvider.System;
        var session = await dbContext.UserSessions.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.Id == sessionId
            && candidate.UserAccountId == userAccountId
            && candidate.RevokedAt == null,
            resource.CancellationToken);
        if (session is null)
        {
            return;
        }

        if (!session.IsSharedDisplay
            || (session.AdministrativeElevationHouseholdId == resource.HouseholdId
                && session.AdministrativeElevationExpiresAt > timeProvider.GetUtcNow()))
        {
            context.Succeed(requirement);
        }
    }
}
