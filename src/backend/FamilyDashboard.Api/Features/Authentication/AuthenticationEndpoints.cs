using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/login/google", BeginGoogleLoginAsync)
            .AllowAnonymous();
        endpoints.MapGet("/api/auth/google/complete", CompleteGoogleLoginAsync)
            .AllowAnonymous();
        endpoints.MapGet("/api/auth/me", GetCurrentUserAsync)
            .RequireAuthorization();
        endpoints.MapGet("/api/auth/antiforgery", GetAntiforgeryToken)
            .RequireAuthorization();
        endpoints.MapPut("/api/auth/session/household", SelectHouseholdAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapPost("/api/auth/logout", LogoutAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> BeginGoogleLoginAsync(
        string? returnUrl,
        HttpContext context,
        IAuthenticationSchemeProvider schemeProvider,
        bool chooseAccount = false)
    {
        if (!ReturnUrlValidator.TryNormalize(returnUrl, out var normalizedReturnUrl))
        {
            return Results.Problem(ApiProblems.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiProblemCodes.InvalidReturnUrl,
                "The return URL must be a local application path."));
        }

        if (await schemeProvider.GetSchemeAsync(AuthenticationSchemes.Google) is null)
        {
            return Results.Problem(ApiProblems.Create(
                context,
                StatusCodes.Status503ServiceUnavailable,
                ApiProblemCodes.AuthenticationUnavailable,
                "Google authentication is not configured."));
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/auth/google/complete",
        };
        properties.Items["returnUrl"] = normalizedReturnUrl;
        if (chooseAccount)
        {
            properties.Parameters["prompt"] = "select_account";
        }
        return Results.Challenge(properties, [AuthenticationSchemes.Google]);
    }

    private static async Task<IResult> CompleteGoogleLoginAsync(
        HttpContext context,
        GoogleSignInService signInService,
        IOptions<AuthenticationConfiguration> options,
        CancellationToken cancellationToken)
    {
        var externalResult = await context.AuthenticateAsync(AuthenticationSchemes.ExternalCookie);
        if (!externalResult.Succeeded || externalResult.Principal is null)
        {
            return AuthenticationFailureRedirect(options.Value.FrontendOrigin);
        }

        string? returnUrl = null;
        externalResult.Properties?.Items.TryGetValue("returnUrl", out returnUrl);
        if (!ReturnUrlValidator.TryNormalize(returnUrl, out var normalizedReturnUrl))
        {
            await context.SignOutAsync(AuthenticationSchemes.ExternalCookie);
            return AuthenticationFailureRedirect(options.Value.FrontendOrigin);
        }

        var result = await signInService.SignInAsync(externalResult.Principal, cancellationToken);
        await context.SignOutAsync(AuthenticationSchemes.ExternalCookie);
        if (result.Status != GoogleSignInStatus.Success || result.Session is null)
        {
            return AuthenticationFailureRedirect(options.Value.FrontendOrigin);
        }

        await context.SignInAsync(
            AuthenticationSchemes.ApplicationCookie,
            UserSessionService.CreatePrincipal(result.Session),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = false,
                IssuedUtc = result.Session.CreatedAt,
                ExpiresUtc = result.Session.ExpiresAt,
            });
        return Results.Redirect(
            $"{options.Value.FrontendOrigin.TrimEnd('/')}{normalizedReturnUrl}",
            permanent: false,
            preserveMethod: false);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        HouseholdService householdService,
        UserSessionService sessionService,
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
        var session = await sessionService.FindCurrentAsync(context.User, cancellationToken);
        Guid? selectedHouseholdId = session?.SelectedHouseholdId is Guid selected
            && households.Any(household => household.Id == selected)
                ? selected
                : null;
        return Results.Ok(new CurrentUserResponse(
            new UserAccountResponse(account.Id, account.DisplayName, account.PrimaryEmail),
            households,
            selectedHouseholdId,
            session is null
                ? null
                : MapSession(session)));
    }

    private static IResult GetAntiforgeryToken(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new AntiforgeryTokenResponse(
            tokens.RequestToken!,
            tokens.HeaderName!));
    }

    private static async Task<IResult> SelectHouseholdAsync(
        SelectHouseholdRequest? request,
        HttpContext context,
        UserSessionService sessionService,
        CancellationToken cancellationToken)
    {
        if (request is null || request.HouseholdId == Guid.Empty)
        {
            return HouseholdEndpoints.ValidationFailed(
                context,
                new Dictionary<string, string[]>
                {
                    ["householdId"] = ["A household is required."],
                });
        }

        var result = await sessionService.SelectHouseholdAsync(
            context.User,
            request.HouseholdId,
            cancellationToken);
        return result.Status switch
        {
            HouseholdSelectionStatus.Success => Results.Ok(result.Selection),
            HouseholdSelectionStatus.SessionUnavailable => AccountUnavailable(context),
            HouseholdSelectionStatus.HouseholdNotFound => HouseholdEndpoints.HouseholdNotFound(context),
            _ => throw new InvalidOperationException("Unsupported household selection result."),
        };
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        UserSessionService sessionService,
        CancellationToken cancellationToken)
    {
        await sessionService.RevokeCurrentAsync(context.User, cancellationToken);
        await context.SignOutAsync(AuthenticationSchemes.ApplicationCookie);
        await context.SignOutAsync(AuthenticationSchemes.ExternalCookie);
        return Results.NoContent();
    }

    private static IResult AuthenticationFailureRedirect(string frontendOrigin) =>
        Results.Redirect(
            $"{frontendOrigin.TrimEnd('/')}/auth/error?code={ApiProblemCodes.AuthenticationFailed}",
            permanent: false,
            preserveMethod: false);

    private static IResult AccountUnavailable(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status401Unauthorized,
            ApiProblemCodes.AccountUnavailable,
            "The authenticated account is unavailable."));
    }

    internal static CurrentSessionResponse MapSession(
        FamilyDashboard.Api.Domain.Identity.UserSession session) => new(
            session.ExpiresAt,
            session.IsSharedDisplay,
            session.DeviceLabel,
            session.AdministrativeElevationHouseholdId,
            session.AdministrativeElevationExpiresAt);
}
