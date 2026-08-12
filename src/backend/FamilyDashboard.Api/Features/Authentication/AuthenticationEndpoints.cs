using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;

namespace FamilyDashboard.Api.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/me", GetCurrentUserAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        HouseholdService householdService,
        CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId))
        {
            return AccountUnavailable(context);
        }

        var account = await householdService.FindActiveAccountAsync(userAccountId, cancellationToken);
        if (account is null)
        {
            return AccountUnavailable(context);
        }

        var households = await householdService.ListAsync(userAccountId, cancellationToken);
        return Results.Ok(new CurrentUserResponse(
            new UserAccountResponse(account.Id, account.DisplayName, account.PrimaryEmail),
            households,
            SelectedHouseholdId: null));
    }

    private static IResult AccountUnavailable(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status401Unauthorized,
            ApiProblemCodes.AccountUnavailable,
            "The authenticated account is unavailable."));
    }
}
