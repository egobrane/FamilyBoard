using System.Security.Claims;

namespace FamilyDashboard.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserAccountId(this ClaimsPrincipal principal, out Guid userAccountId)
    {
        return Guid.TryParse(
            principal.FindFirstValue(FamilyDashboardClaimTypes.UserAccountId),
            out userAccountId);
    }

    public static bool TryGetUserSessionId(this ClaimsPrincipal principal, out Guid userSessionId)
    {
        return Guid.TryParse(
            principal.FindFirstValue(FamilyDashboardClaimTypes.UserSessionId),
            out userSessionId);
    }
}
