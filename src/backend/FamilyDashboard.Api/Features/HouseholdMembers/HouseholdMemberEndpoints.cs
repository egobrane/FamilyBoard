using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Features.Dashboard;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        endpoints.MapPost("/api/households/{householdId:guid}/members/{memberId:guid}/photo", UploadPhotoAsync)
            .DisableAntiforgery()
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapPut("/api/households/{householdId:guid}/members/{memberId:guid}/photo-position", UpdatePhotoPositionAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapDelete("/api/households/{householdId:guid}/members/{memberId:guid}/photo", RemovePhotoAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/households/{householdId:guid}/members/{memberId:guid}/photo/{assetId:guid}/{variant}", GetPhotoAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> UploadPhotoAsync(
        Guid householdId, Guid memberId, HttpContext context, IAuthorizationService authorizationService,
        HouseholdService householdService, HouseholdMemberPhotoService photoService,
        FamilyDashboardDbContext dbContext, CancellationToken cancellationToken)
    {
        var denial = await AuthorizeAsync(context, authorizationService, householdService, householdId, true, cancellationToken);
        if (denial is not null) return denial;
        if (!context.Request.HasFormContentType)
            return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["photo"] = ["Choose a photo to upload."] });
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var photo = form.Files.GetFile("photo");
        if (photo is null)
            return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["photo"] = ["Choose a photo to upload."] });
        if (!long.TryParse(form["expectedPhotoVersion"], out var expectedPhotoVersion) || expectedPhotoVersion <= 0)
            return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["expectedPhotoVersion"] = ["Refresh the member and try again."] });
        var actorMemberId = await CurrentMemberIdAsync(context, householdId, dbContext, cancellationToken);
        if (actorMemberId is null) return HouseholdEndpoints.HouseholdNotFound(context);
        try
        {
            await using var stream = photo.OpenReadStream();
            return MapPhotoMutation(await photoService.UploadAsync(householdId, memberId, actorMemberId.Value,
                expectedPhotoVersion, stream, photo.Length, cancellationToken), context);
        }
        catch (InvalidHouseholdPhotoException exception)
        {
            return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["photo"] = [exception.Message] });
        }
        catch (HouseholdMediaUnavailableException)
        {
            return Results.Problem(ApiProblems.Create(context, 503, ApiProblemCodes.HouseholdMediaUnavailable,
                "Household photo storage is currently unavailable."));
        }
    }

    private static async Task<IResult> UpdatePhotoPositionAsync(
        Guid householdId, Guid memberId, UpdateHouseholdMemberPhotoPositionRequest? request,
        HttpContext context, IAuthorizationService authorizationService, HouseholdService householdService,
        HouseholdMemberPhotoService photoService, CancellationToken cancellationToken)
    {
        var denial = await AuthorizeAsync(context, authorizationService, householdService, householdId, true, cancellationToken);
        if (denial is not null) return denial;
        var errors = new Dictionary<string, string[]>();
        if (request is null) errors["request"] = ["A photo-position request is required."];
        else
        {
            if (request.ExpectedPhotoVersion <= 0) errors["expectedPhotoVersion"] = ["Refresh the member and try again."];
            if (request.FocalX is < 0 or > 1) errors["focalX"] = ["Horizontal position must be between 0 and 1."];
            if (request.FocalY is < 0 or > 1) errors["focalY"] = ["Vertical position must be between 0 and 1."];
        }
        if (errors.Count > 0) return HouseholdEndpoints.ValidationFailed(context, errors);
        return MapPhotoMutation(await photoService.UpdatePositionAsync(householdId, memberId, request!, cancellationToken), context);
    }

    private static async Task<IResult> RemovePhotoAsync(
        Guid householdId, Guid memberId, [FromBody] RemoveHouseholdMemberPhotoRequest? request,
        HttpContext context, IAuthorizationService authorizationService, HouseholdService householdService,
        HouseholdMemberPhotoService photoService, CancellationToken cancellationToken)
    {
        var denial = await AuthorizeAsync(context, authorizationService, householdService, householdId, true, cancellationToken);
        if (denial is not null) return denial;
        if (request is null || request.ExpectedPhotoVersion <= 0)
            return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["expectedPhotoVersion"] = ["Refresh the member and try again."] });
        return MapPhotoMutation(await photoService.RemoveAsync(householdId, memberId, request.ExpectedPhotoVersion, cancellationToken), context);
    }

    private static async Task<IResult> GetPhotoAsync(
        Guid householdId, Guid memberId, Guid assetId, string variant, HttpContext context,
        IAuthorizationService authorizationService, HouseholdService householdService,
        HouseholdMemberPhotoService photoService, CancellationToken cancellationToken)
    {
        var denial = await AuthorizeAsync(context, authorizationService, householdService, householdId, false, cancellationToken);
        if (denial is not null) return denial;
        var photo = await photoService.ReadAsync(householdId, memberId, assetId, variant.ToLowerInvariant(), cancellationToken);
        if (photo is null) return Results.NotFound();
        if (context.Request.Headers.IfNoneMatch.Contains(photo.ETag)) return Results.StatusCode(StatusCodes.Status304NotModified);
        context.Response.Headers.CacheControl = "private, no-cache";
        context.Response.Headers.ETag = photo.ETag;
        return Results.Stream(photo.Content, photo.ContentType, enableRangeProcessing: true);
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
            requireAdult: true,
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

            var administrationAccess = await HouseholdEndpoints.HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Administration,
                cancellationToken);
            if (!administrationAccess)
            {
                return ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context);
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

    private static IResult MapPhotoMutation(HouseholdMemberPhotoMutationResult result, HttpContext context) => result.Status switch
    {
        HouseholdMemberPhotoMutationStatus.Success => Results.Ok(result.Member),
        HouseholdMemberPhotoMutationStatus.MemberNotFound => MemberNotFound(context),
        HouseholdMemberPhotoMutationStatus.Conflict => Results.Problem(ApiProblems.Create(context, 409,
            ApiProblemCodes.HouseholdMemberPhotoConflict, "This member photo changed. Refresh and try again.")),
        _ => throw new InvalidOperationException("Unsupported member photo result."),
    };

    private static async Task<Guid?> CurrentMemberIdAsync(HttpContext context, Guid householdId,
        FamilyDashboardDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var accountId)) return null;
        return await dbContext.HouseholdMemberships.Where(value => value.UserAccountId == accountId
            && value.HouseholdId == householdId && value.HouseholdMember.IsActive)
            .Select(value => (Guid?)value.HouseholdMemberId).SingleOrDefaultAsync(cancellationToken);
    }
}
