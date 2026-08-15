using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Invitations;

public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/households/{householdId:guid}/invitations", CreateAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/households/{householdId:guid}/invitations", ListAsync)
            .RequireAuthorization();
        endpoints.MapPost(
                "/api/households/{householdId:guid}/invitations/{invitationId:guid}/revoke",
                RevokeAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapPost("/api/invitations/prepare", PrepareAsync).AllowAnonymous();
        endpoints.MapGet("/api/invitations/pending", GetPendingAsync).AllowAnonymous();
        endpoints.MapPost("/api/invitations/pending/accept", AcceptAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid householdId,
        CreateInvitationRequest? request,
        HttpContext context,
        IAuthorizationService authorizationService,
        InvitationService service,
        CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId))
            return HouseholdEndpoints.AccountUnavailable(context);
        var access = await AuthorizeAdultAsync(
            context, authorizationService, householdId, cancellationToken);
        if (access is not null) return access;
        if (!InvitationValidation.TryNormalizeEmail(request, out var email, out var errors))
            return HouseholdEndpoints.ValidationFailed(context, errors);

        var result = await service.CreateAsync(
            householdId, userAccountId, email!, cancellationToken);
        return result.Status switch
        {
            InvitationOperationStatus.Success => Results.Created(
                $"/api/households/{householdId}/invitations/{result.Value!.Invitation.Id}",
                result.Value),
            InvitationOperationStatus.Conflict => Problem(
                context, 409, ApiProblemCodes.ActiveInvitationExists,
                "A pending invitation already exists for this email address."),
            _ => throw new InvalidOperationException("Unsupported invitation creation result."),
        };
    }

    private static async Task<IResult> ListAsync(
        Guid householdId,
        HttpContext context,
        IAuthorizationService authorizationService,
        InvitationService service,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAdultAsync(
            context, authorizationService, householdId, cancellationToken);
        return access ?? Results.Ok(await service.ListAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> RevokeAsync(
        Guid householdId,
        Guid invitationId,
        HttpContext context,
        IAuthorizationService authorizationService,
        InvitationService service,
        CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId))
            return HouseholdEndpoints.AccountUnavailable(context);
        var access = await AuthorizeAdultAsync(
            context, authorizationService, householdId, cancellationToken);
        if (access is not null) return access;
        var result = await service.RevokeAsync(
            householdId, invitationId, userAccountId, cancellationToken);
        return Result(context, result, success => Results.Ok(success));
    }

    private static async Task<IResult> PrepareAsync(
        PrepareInvitationRequest? request,
        HttpContext context,
        InvitationService service,
        PendingInvitationCookieService cookieService,
        IOptions<AuthenticationConfiguration> authenticationOptions,
        CancellationToken cancellationToken)
    {
        if (!IsTrustedJsonRequest(context.Request, authenticationOptions.Value.FrontendOrigin))
            return Problem(
                context, 403, ApiProblemCodes.InvitationOriginNotAllowed,
                "Invitation preparation is only available to the configured frontend origin.");
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
            return HouseholdEndpoints.ValidationFailed(
                context,
                new Dictionary<string, string[]> { ["token"] = ["An invitation token is required."] });

        var result = await service.PrepareAsync(request.Token, cancellationToken);
        if (result.Status == InvitationOperationStatus.Success)
        {
            cookieService.Set(context.Response, result.Value!.Id, result.Value.Response.ExpiresAt);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result.Value.Response);
        }
        cookieService.Delete(context.Response);
        return InvitationUnavailable(context, result.Status);
    }

    private static async Task<IResult> GetPendingAsync(
        HttpContext context,
        InvitationService service,
        PendingInvitationCookieService cookieService,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!cookieService.TryRead(context.Request, out var invitationId))
            return InvitationUnavailable(context, InvitationOperationStatus.NotFound);
        var result = await service.GetPendingAsync(invitationId, cancellationToken);
        if (result.Status == InvitationOperationStatus.Success) return Results.Ok(result.Value);
        cookieService.Delete(context.Response);
        return InvitationUnavailable(context, result.Status);
    }

    private static async Task<IResult> AcceptAsync(
        HttpContext context,
        InvitationService service,
        PendingInvitationCookieService cookieService,
        CancellationToken cancellationToken)
    {
        if (!cookieService.TryRead(context.Request, out var invitationId))
            return InvitationUnavailable(context, InvitationOperationStatus.NotFound);
        if (!context.User.TryGetUserAccountId(out var userAccountId)
            || !context.User.TryGetUserSessionId(out var sessionId))
            return HouseholdEndpoints.AccountUnavailable(context);

        var result = await service.AcceptAsync(
            invitationId, userAccountId, sessionId, cancellationToken);
        if (result.Status == InvitationOperationStatus.Success)
        {
            cookieService.Delete(context.Response);
            return Results.Ok(result.Value);
        }
        if (result.Status is InvitationOperationStatus.Expired
            or InvitationOperationStatus.Revoked
            or InvitationOperationStatus.Used
            or InvitationOperationStatus.NotFound)
            cookieService.Delete(context.Response);
        return result.Status switch
        {
            InvitationOperationStatus.EmailMismatch => Problem(
                context, 403, ApiProblemCodes.InvitationEmailMismatch,
                "Sign in with the Google account named by this invitation."),
            InvitationOperationStatus.SessionUnavailable => HouseholdEndpoints.AccountUnavailable(context),
            InvitationOperationStatus.Conflict => Problem(
                context, 409, ApiProblemCodes.InvitationConflict,
                "The invitation changed at the same time. Try again."),
            _ => InvitationUnavailable(context, result.Status),
        };
    }

    private static async Task<IResult?> AuthorizeAdultAsync(
        HttpContext context,
        IAuthorizationService authorizationService,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        if (!await HouseholdEndpoints.HasAccessAsync(
                context, authorizationService, householdId,
                HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        if (!await HouseholdEndpoints.HasAccessAsync(
                context, authorizationService, householdId,
                HouseholdAuthorizationPolicies.Adult, cancellationToken))
            return HouseholdEndpoints.AdultAccessRequired(context);
        return null;
    }

    private static IResult Result<T>(
        HttpContext context,
        InvitationOperationResult<T> result,
        Func<T, IResult> success) => result.Status switch
        {
            InvitationOperationStatus.Success => success(result.Value!),
            InvitationOperationStatus.Conflict => Problem(
                context, 409, ApiProblemCodes.InvitationConflict,
                "The invitation changed at the same time. Try again."),
            _ => InvitationUnavailable(context, result.Status),
        };

    private static IResult InvitationUnavailable(
        HttpContext context,
        InvitationOperationStatus status) => status switch
        {
            InvitationOperationStatus.NotFound => Problem(
                context, 404, ApiProblemCodes.InvitationNotFound, "The invitation was not found."),
            InvitationOperationStatus.Expired => Problem(
                context, 410, ApiProblemCodes.InvitationExpired, "The invitation has expired."),
            InvitationOperationStatus.Revoked => Problem(
                context, 410, ApiProblemCodes.InvitationRevoked, "The invitation was revoked."),
            InvitationOperationStatus.Used => Problem(
                context, 410, ApiProblemCodes.InvitationUsed, "The invitation has already been used."),
            _ => Problem(
                context, 410, ApiProblemCodes.InvitationUnavailable, "The invitation is unavailable."),
        };

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(ApiProblems.Create(context, status, code, title));

    private static bool IsTrustedJsonRequest(HttpRequest request, string frontendOrigin) =>
        request.HasJsonContentType()
        && request.Headers.Origin.Count == 1
        && string.Equals(
            request.Headers.Origin[0]?.TrimEnd('/'),
            frontendOrigin.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
}
