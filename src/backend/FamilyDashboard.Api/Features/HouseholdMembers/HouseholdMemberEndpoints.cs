using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace FamilyDashboard.Api.Features.HouseholdMembers;

public static class HouseholdMemberEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/households/{householdId:guid}/members", ListMembersAsync)
            .RequireAuthorization();
        endpoints.MapPost("/api/households/{householdId:guid}/members", CreateChildAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapPatch(
                "/api/households/{householdId:guid}/members/{memberId:guid}",
                UpdateMemberAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> ListMembersAsync(
        Guid householdId,
        HttpContext context,
        IAuthorizationService authorizationService,
        HouseholdService householdService,
        HouseholdMemberService memberService,
        CancellationToken cancellationToken)
    {
        var authorizationFailure = await AuthorizeAsync(
            context,
            authorizationService,
            householdService,
            householdId,
            requireAdult: false,
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        return Results.Ok(await memberService.ListAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> CreateChildAsync(
        Guid householdId,
        CreateChildMemberRequest? request,
        HttpContext context,
        IAuthorizationService authorizationService,
        HouseholdService householdService,
        HouseholdMemberService memberService,
        CancellationToken cancellationToken)
    {
        var authorizationFailure = await AuthorizeAsync(
            context,
            authorizationService,
            householdService,
            householdId,
            requireAdult: true,
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        if (!HouseholdMemberValidation.TryValidate(
                request,
                out var displayName,
                out var avatarColor,
                out var errors))
        {
            return HouseholdEndpoints.ValidationFailed(context, errors);
        }

        var response = await memberService.CreateChildAsync(
            householdId,
            displayName!,
            avatarColor,
            cancellationToken);
        return response is null
            ? HouseholdEndpoints.HouseholdNotFound(context)
            : Results.Created(
                $"/api/households/{householdId}/members/{response.Id}",
                response);
    }

    private static async Task<IResult> UpdateMemberAsync(
        Guid householdId,
        Guid memberId,
        UpdateHouseholdMemberRequest? request,
        HttpContext context,
        IAuthorizationService authorizationService,
        HouseholdService householdService,
        HouseholdMemberService memberService,
        CancellationToken cancellationToken)
    {
        var authorizationFailure = await AuthorizeAsync(
            context,
            authorizationService,
            householdService,
            householdId,
            requireAdult: true,
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        if (!context.User.TryGetUserAccountId(out var actorUserAccountId))
        {
            return HouseholdEndpoints.AccountUnavailable(context);
        }

        if (!HouseholdMemberValidation.TryValidate(request, out var patch, out var errors))
        {
            return HouseholdEndpoints.ValidationFailed(context, errors);
        }

        var result = await memberService.UpdateAsync(
            householdId,
            memberId,
            actorUserAccountId,
            patch!,
            cancellationToken);
        return result.Status switch
        {
            HouseholdMemberUpdateStatus.Success => Results.Ok(result.Member),
            HouseholdMemberUpdateStatus.NotFound => MemberNotFound(context),
            HouseholdMemberUpdateStatus.LastActiveAdult => LastActiveAdult(context),
            HouseholdMemberUpdateStatus.SelfDeactivationRequiresLeaveFlow =>
                SelfDeactivationRequiresLeaveFlow(context),
            HouseholdMemberUpdateStatus.Conflict => Conflict(context),
            _ => throw new InvalidOperationException("Unsupported household member update result."),
        };
    }

    private static async Task<IResult?> AuthorizeAsync(
        HttpContext context,
        IAuthorizationService authorizationService,
        HouseholdService householdService,
        Guid householdId,
        bool requireAdult,
        CancellationToken cancellationToken)
    {
        var userAccountId = await HouseholdEndpoints.ResolveAccountAsync(
            context,
            householdService,
            cancellationToken);
        if (userAccountId is null)
        {
            return HouseholdEndpoints.AccountUnavailable(context);
        }

        var memberAccess = await HouseholdEndpoints.HasAccessAsync(
            context,
            authorizationService,
            householdId,
            HouseholdAuthorizationPolicies.Member,
            cancellationToken);
        if (!memberAccess)
        {
            return HouseholdEndpoints.HouseholdNotFound(context);
        }

        if (requireAdult)
        {
            var adultAccess = await HouseholdEndpoints.HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Adult,
                cancellationToken);
            if (!adultAccess)
            {
                return HouseholdEndpoints.AdultAccessRequired(context);
            }
        }

        return null;
    }

    private static IResult MemberNotFound(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status404NotFound,
            ApiProblemCodes.HouseholdMemberNotFound,
            "The household member was not found."));
    }

    private static IResult LastActiveAdult(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status409Conflict,
            ApiProblemCodes.LastActiveAdult,
            "The last active adult cannot be deactivated."));
    }

    private static IResult SelfDeactivationRequiresLeaveFlow(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status409Conflict,
            ApiProblemCodes.SelfDeactivationRequiresLeaveFlow,
            "Leaving a household requires the dedicated leave-household workflow."));
    }

    private static IResult Conflict(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status409Conflict,
            ApiProblemCodes.Conflict,
            "The household changed concurrently. Retry the request."));
    }
}
