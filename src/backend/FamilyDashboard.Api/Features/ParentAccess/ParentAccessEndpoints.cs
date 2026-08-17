using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.ParentAccess;

public static class ParentAccessEndpoints
{
    public static IEndpointRouteBuilder MapParentAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/households/{householdId:guid}/parent-access", GetStateAsync)
            .RequireAuthorization();
        endpoints.MapPut("/api/households/{householdId:guid}/parent-access/pin", SetPinAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapPost("/api/households/{householdId:guid}/parent-access/pin/recover", RecoverPinAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapPost("/api/households/{householdId:guid}/parent-access/verify", VerifyAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery()
            .RequireRateLimiting("parent-pin-verification");
        endpoints.MapPost("/api/households/{householdId:guid}/parent-access/lock", LockAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapPut("/api/auth/session/shared-display", UpdateSharedDisplayAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> GetStateAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        ParentAccessService service, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAdultAsync(householdId, context, authorization, cancellationToken);
        return failure ?? Result(context,
            await service.GetStateAsync(householdId, context.User, cancellationToken));
    }

    private static async Task<IResult> SetPinAsync(
        Guid householdId, SetParentPinRequest? request, HttpContext context,
        IAuthorizationService authorization, ParentAccessService service,
        IOptions<ParentAccessConfiguration> options, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAdultAsync(householdId, context, authorization, cancellationToken);
        if (failure is not null) return failure;
        if (!ParentAccessValidation.TryValidatePin(request?.Pin, options.Value, out var errors))
            return HouseholdEndpoints.ValidationFailed(context, errors);
        return Result(context, await service.SetupOrChangeAsync(
            householdId, context.User, request!.Pin, context.TraceIdentifier, cancellationToken));
    }

    private static async Task<IResult> RecoverPinAsync(
        Guid householdId, SetParentPinRequest? request, HttpContext context,
        IAuthorizationService authorization, ParentAccessService service,
        IOptions<ParentAccessConfiguration> options, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAdultAsync(householdId, context, authorization, cancellationToken);
        if (failure is not null) return failure;
        if (!ParentAccessValidation.TryValidatePin(request?.Pin, options.Value, out var errors))
            return HouseholdEndpoints.ValidationFailed(context, errors);
        return Result(context, await service.RecoverAsync(
            householdId, context.User, request!.Pin, context.TraceIdentifier, cancellationToken));
    }

    private static async Task<IResult> VerifyAsync(
        Guid householdId, VerifyParentPinRequest? request, HttpContext context,
        IAuthorizationService authorization, ParentAccessService service,
        IOptions<ParentAccessConfiguration> options, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAdultAsync(householdId, context, authorization, cancellationToken);
        if (failure is not null) return failure;
        if (!ParentAccessValidation.TryValidatePin(request?.Pin, options.Value, out var errors))
            return HouseholdEndpoints.ValidationFailed(context, errors);
        return Result(context, await service.VerifyAsync(
            householdId, context.User, request!.Pin, context.TraceIdentifier, cancellationToken));
    }

    private static async Task<IResult> LockAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        ParentAccessService service, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAdultAsync(householdId, context, authorization, cancellationToken);
        if (failure is not null) return failure;
        return Result(context, await service.LockAsync(
            householdId, context.User, context.TraceIdentifier, cancellationToken), noContent: true);
    }

    private static async Task<IResult> UpdateSharedDisplayAsync(
        UpdateSharedDisplayRequest? request, HttpContext context,
        IAuthorizationService authorization, ParentAccessService service,
        UserSessionService sessionService, CancellationToken cancellationToken)
    {
        if (request is null || request.HouseholdId == Guid.Empty)
            return HouseholdEndpoints.ValidationFailed(context,
                new Dictionary<string, string[]> { ["householdId"] = ["A household is required."] });
        if (!ParentAccessValidation.TryValidateDeviceLabel(
                request.DeviceLabel, out var deviceLabel, out var errors))
            return HouseholdEndpoints.ValidationFailed(context, errors);
        var failure = await AuthorizeAdultAsync(
            request.HouseholdId, context, authorization, cancellationToken);
        if (failure is not null) return failure;

        var result = await service.UpdateSharedDisplayAsync(
            request.HouseholdId, context.User, request.IsSharedDisplay, deviceLabel,
            context.TraceIdentifier, cancellationToken);
        if (result.Status != ParentAccessOperationStatus.Success) return Result(context, result);

        var session = await sessionService.FindCurrentForUpdateAsync(context.User, cancellationToken);
        if (session is null) return HouseholdEndpoints.AccountUnavailable(context);
        await context.SignInAsync(
            AuthenticationSchemes.ApplicationCookie,
            UserSessionService.CreatePrincipal(session),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = false,
                IssuedUtc = session.CreatedAt,
                ExpiresUtc = session.ExpiresAt,
            });
        return Results.Ok(AuthenticationEndpoints.MapSession(session));
    }

    private static async Task<IResult?> AuthorizeAdultAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        if (!await HouseholdEndpoints.HasAccessAsync(
                context, authorization, householdId, HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        if (!await HouseholdEndpoints.HasAccessAsync(
                context, authorization, householdId, HouseholdAuthorizationPolicies.Adult, cancellationToken))
            return HouseholdEndpoints.AdultAccessRequired(context);
        return null;
    }

    public static IResult ParentElevationRequired(HttpContext context) => Problem(
        context, StatusCodes.Status403Forbidden, ApiProblemCodes.ParentElevationRequired,
        "Recent parent PIN verification is required.");

    private static IResult Result(
        HttpContext context, ParentAccessOperationResult result, bool noContent = false)
    {
        if (result.Status == ParentAccessOperationStatus.Success)
            return noContent ? Results.NoContent() : Results.Ok(result.State);

        if (result.Status == ParentAccessOperationStatus.Locked && result.RetryAt is { } retryAt)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling((retryAt - DateTimeOffset.UtcNow).TotalSeconds));
            context.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var problem = ApiProblems.Create(context, 429, ApiProblemCodes.ParentPinLocked,
                "Parent access is temporarily locked.");
            problem.Extensions["retryAfterSeconds"] = seconds;
            return Results.Problem(problem);
        }

        return result.Status switch
        {
            ParentAccessOperationStatus.Unavailable => Problem(context, 503,
                ApiProblemCodes.ParentAccessUnavailable, "Parent access is not configured."),
            ParentAccessOperationStatus.SessionUnavailable => HouseholdEndpoints.AccountUnavailable(context),
            ParentAccessOperationStatus.HouseholdNotFound => HouseholdEndpoints.HouseholdNotFound(context),
            ParentAccessOperationStatus.PinNotConfigured => Problem(context, 409,
                ApiProblemCodes.ParentPinNotConfigured, "Set a parent PIN before continuing."),
            ParentAccessOperationStatus.PinAlreadyConfigured => Problem(context, 409,
                ApiProblemCodes.ParentPinAlreadyConfigured, "A parent PIN is already configured."),
            ParentAccessOperationStatus.InvalidPin => Problem(context, 403,
                ApiProblemCodes.ParentPinInvalid, "The parent PIN could not be verified."),
            ParentAccessOperationStatus.ElevationRequired => ParentElevationRequired(context),
            ParentAccessOperationStatus.RecentAuthenticationRequired => Problem(context, 403,
                ApiProblemCodes.RecentAuthenticationRequired, "Sign out and sign in again before recovering the parent PIN."),
            ParentAccessOperationStatus.PrivateSessionRequired => Problem(context, 403,
                ApiProblemCodes.PrivateSessionRequired, "Use a private adult session for this action."),
            ParentAccessOperationStatus.SharedDisplayRequiresPin => Problem(context, 409,
                ApiProblemCodes.SharedDisplayRequiresPin, "Set a parent PIN before enabling shared-display mode."),
            ParentAccessOperationStatus.Conflict => Problem(context, 409,
                ApiProblemCodes.ParentAccessConflict, "Parent access changed concurrently. Try again."),
            _ => Problem(context, 409, ApiProblemCodes.ParentAccessConflict,
                "Parent access could not be updated."),
        };
    }

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(ApiProblems.Create(context, status, code, title));
}
