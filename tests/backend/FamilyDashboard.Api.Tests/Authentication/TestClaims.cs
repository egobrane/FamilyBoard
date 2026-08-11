using System.Security.Claims;
using FamilyDashboard.Api.Security;

namespace FamilyDashboard.Api.Tests.Authentication;

internal static class TestClaims
{
    public static ClaimsPrincipal AuthenticatedAdult(Guid userAccountId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(FamilyDashboardClaimTypes.UserAccountId, userAccountId.ToString())],
            TestAuthenticationHandler.SchemeName);

        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal AuthenticatedWithoutUserAccountId()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(authenticationType: TestAuthenticationHandler.SchemeName));
    }

    public static ClaimsPrincipal Anonymous()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}
