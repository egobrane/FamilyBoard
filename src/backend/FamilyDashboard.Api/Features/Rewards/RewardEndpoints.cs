using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace FamilyDashboard.Api.Features.Rewards;

public static class RewardEndpoints
{
    public static IEndpointRouteBuilder MapRewardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/households/{householdId:guid}").RequireAuthorization();
        group.MapGet("/rewards", Catalog);
        group.MapGet("/reward-redemptions", ListRedemptions);
        group.MapPost("/reward-redemptions", Request).RequireFamilyDashboardAntiforgery();
        group.MapGet("/reward-definitions", Definitions);
        group.MapPost("/reward-definitions", CreateDefinition).RequireFamilyDashboardAntiforgery();
        group.MapPatch("/reward-definitions/{rewardId:guid}", UpdateDefinition).RequireFamilyDashboardAntiforgery();
        group.MapPost("/reward-definitions/{rewardId:guid}/activate", (Guid householdId, Guid rewardId,
            ChangeRewardStateRequest? request, HttpContext context, IAuthorizationService auth,
            HouseholdService households, RewardService rewards, CancellationToken token) =>
            ChangeState(householdId, rewardId, true, request, context, auth, households, rewards, token))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/reward-definitions/{rewardId:guid}/deactivate", (Guid householdId, Guid rewardId,
            ChangeRewardStateRequest? request, HttpContext context, IAuthorizationService auth,
            HouseholdService households, RewardService rewards, CancellationToken token) =>
            ChangeState(householdId, rewardId, false, request, context, auth, households, rewards, token))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/reward-redemptions/{redemptionId:guid}/review", Review).RequireFamilyDashboardAntiforgery();
        group.MapPost("/reward-redemptions/{redemptionId:guid}/fulfill", Fulfill).RequireFamilyDashboardAntiforgery();
        group.MapPost("/reward-redemptions/{redemptionId:guid}/cancel", Cancel).RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> Catalog(Guid householdId, HttpContext context,
        IAuthorizationService auth, HouseholdService households, RewardService rewards, CancellationToken token)
    {
        var failure = await Authorize(householdId, false, context, auth, households, token);
        return failure ?? Results.Ok(await rewards.GetCatalogAsync(householdId, token));
    }

    private static async Task<IResult> Definitions(Guid householdId, HttpContext context,
        IAuthorizationService auth, HouseholdService households, RewardService rewards, CancellationToken token)
    {
        var failure = await Authorize(householdId, true, context, auth, households, token);
        return failure ?? Results.Ok(await rewards.ListDefinitionsAsync(householdId, token));
    }

    private static async Task<IResult> CreateDefinition(Guid householdId, CreateRewardRequest? request,
        HttpContext context, IAuthorizationService auth, HouseholdService households, RewardService rewards,
        CancellationToken token)
    {
        var failure = await Authorize(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor)) return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null) return Validation(context,
            new Dictionary<string, string[]> { ["reward"] = ["Reward data is required."] });
        if (!RewardValidation.TryDefinition(request.ClientRequestId, request.Title,
                request.Description, request.PointCost, out var clean, out var errors)) return Validation(context, errors);
        return Result(context, await rewards.CreateDefinitionAsync(householdId, actor, clean!, token), true);
    }

    private static async Task<IResult> UpdateDefinition(Guid householdId, Guid rewardId, UpdateRewardRequest? request,
        HttpContext context, IAuthorizationService auth, HouseholdService households, RewardService rewards,
        CancellationToken token)
    {
        var failure = await Authorize(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor)) return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null || request.ExpectedVersion < 1) return Validation(context,
            new Dictionary<string, string[]> { ["reward"] = ["Reward data and a valid version are required."] });
        if (!RewardValidation.TryDefinition(Guid.NewGuid(), request.Title, request.Description,
                request.PointCost, out var clean, out var errors)) return Validation(context, errors);
        var update = new UpdateRewardRequest(request.ExpectedVersion, clean!.Title, clean.Description, clean.PointCost);
        return Result(context, await rewards.UpdateDefinitionAsync(householdId, rewardId, actor, update, token));
    }

    private static async Task<IResult> ChangeState(Guid householdId, Guid rewardId, bool active,
        ChangeRewardStateRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, RewardService rewards, CancellationToken token)
    {
        var failure = await Authorize(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor)) return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null || request.ExpectedVersion < 1) return Validation(context,
            new Dictionary<string, string[]> { ["expectedVersion"] = ["A valid version is required."] });
        return Result(context, await rewards.SetStateAsync(householdId, rewardId, actor, request.ExpectedVersion, active, token));
    }

    private static async Task<IResult> Request(Guid householdId, CreateRewardRedemptionRequest? request,
        HttpContext context, IAuthorizationService auth, HouseholdService households, RewardService rewards,
        CancellationToken token)
    {
        var failure = await Authorize(householdId, false, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || !context.User.TryGetUserSessionId(out var session)
            || request is null || request.ClientRequestId == Guid.Empty || request.RewardId == Guid.Empty)
            return Validation(context, new Dictionary<string, string[]> { ["redemption"] = ["A reward and request ID are required."] });
        return Result(context, await rewards.RequestAsync(householdId, actor, session, request, token), true);
    }

    private static async Task<IResult> ListRedemptions(Guid householdId, Guid? memberId, string? status,
        string? cursor, int? pageSize, HttpContext context, IAuthorizationService auth,
        HouseholdService households, RewardService rewards, CancellationToken token)
    {
        var failure = await Authorize(householdId, false, context, auth, households, token);
        if (failure is not null) return failure;
        RedemptionStatus? parsed = null;
        if (status is not null)
        {
            if (!Enum.TryParse<RedemptionStatus>(status, true, out var value))
                return Validation(context, new Dictionary<string, string[]> { ["status"] = ["The redemption status is invalid."] });
            parsed = value;
        }
        var offset = DecodeCursor(cursor);
        if (offset is null) return Validation(context, new Dictionary<string, string[]> { ["cursor"] = ["The cursor is invalid."] });
        return Results.Ok(await rewards.ListRedemptionsAsync(householdId, memberId, parsed, offset.Value,
            Math.Clamp(pageSize ?? 50, 1, 100), token));
    }

    private static async Task<IResult> Review(Guid householdId, Guid redemptionId,
        ReviewRewardRedemptionRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, RewardService rewards, CancellationToken token)
    {
        var failure = await Authorize(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || request is null || request.ExpectedVersion < 1
            || request.Decision is not ("approved" or "rejected")) return Validation(context,
            new Dictionary<string, string[]> { ["decision"] = ["Choose approved or rejected and provide a valid version."] });
        if (!RewardValidation.TryNote(request.Note, "note", false, out var note, out var errors)) return Validation(context, errors);
        return Result(context, await rewards.ReviewAsync(householdId, redemptionId, actor, request with { Note = note }, token));
    }

    private static async Task<IResult> Fulfill(Guid householdId, Guid redemptionId,
        FulfillRewardRedemptionRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, RewardService rewards, CancellationToken token)
    {
        var failure = await Authorize(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]> { ["expectedVersion"] = ["A valid version is required."] });
        return Result(context, await rewards.FulfillAsync(householdId, redemptionId, actor, request.ExpectedVersion, token));
    }

    private static async Task<IResult> Cancel(Guid householdId, Guid redemptionId,
        CancelRewardRedemptionRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, RewardService rewards, CancellationToken token)
    {
        var failure = await Authorize(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]> { ["expectedVersion"] = ["A valid version is required."] });
        if (!RewardValidation.TryNote(request.Reason, "reason", true, out var reason, out var errors)) return Validation(context, errors);
        return Result(context, await rewards.CancelAsync(householdId, redemptionId, actor, request.ExpectedVersion, reason!, token));
    }

    private static async Task<IResult?> Authorize(Guid householdId, bool administration, HttpContext context,
        IAuthorizationService auth, HouseholdService households, CancellationToken token)
    {
        if (await HouseholdEndpoints.ResolveAccountAsync(context, households, token) is null)
            return HouseholdEndpoints.AccountUnavailable(context);
        if (!await HouseholdEndpoints.HasAccessAsync(context, auth, householdId, HouseholdAuthorizationPolicies.Member, token))
            return HouseholdEndpoints.HouseholdNotFound(context);
        if (!administration) return null;
        if (!await HouseholdEndpoints.HasAccessAsync(context, auth, householdId, HouseholdAuthorizationPolicies.Adult, token))
            return HouseholdEndpoints.AdultAccessRequired(context);
        if (!await HouseholdEndpoints.HasAccessAsync(context, auth, householdId, HouseholdAuthorizationPolicies.Administration, token))
            return ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context);
        return null;
    }

    private static IResult Result<T>(HttpContext context, RewardOperationResult<T> result, bool created = false) => result.Status switch
    {
        RewardOperationStatus.Success when created => Results.Json(result.Value, statusCode: 201),
        RewardOperationStatus.Success => Results.Ok(result.Value),
        RewardOperationStatus.NotFound => Problem(context, 404, ApiProblemCodes.RewardNotFound, "The reward was not found."),
        RewardOperationStatus.MemberNotFound => Problem(context, 404, ApiProblemCodes.RewardMemberNotFound, "The household member was not found."),
        RewardOperationStatus.Inactive => Problem(context, 409, ApiProblemCodes.RewardInactive, "The reward is inactive."),
        RewardOperationStatus.MemberInactive => Problem(context, 409, ApiProblemCodes.RewardMemberInactive, "Select an active household member."),
        RewardOperationStatus.InsufficientPoints => Problem(context, 409, ApiProblemCodes.RewardInsufficientPoints, "The member does not have enough points."),
        RewardOperationStatus.IdempotencyConflict => Problem(context, 409, ApiProblemCodes.RewardIdempotencyConflict, "That request ID was already used for different reward data."),
        RewardOperationStatus.RedemptionNotFound => Problem(context, 404, ApiProblemCodes.RewardRedemptionNotFound, "The redemption was not found."),
        RewardOperationStatus.RedemptionIdempotencyConflict => Problem(context, 409, ApiProblemCodes.RewardRedemptionIdempotencyConflict, "That request ID was already used for a different redemption."),
        RewardOperationStatus.InvalidTransition => Problem(context, 409, ApiProblemCodes.RewardRedemptionInvalidTransition, "The redemption cannot be changed in its current state."),
        RewardOperationStatus.LegacyRequiresResolution => Problem(context, 409, ApiProblemCodes.RewardRedemptionLegacyRequiresResolution, "This legacy redemption has no point reservation and must be cancelled or rejected."),
        _ => Problem(context, 409, ApiProblemCodes.RewardConcurrencyConflict, "Reward information changed. Refresh and try again."),
    };
    private static int? DecodeCursor(string? cursor) { if (cursor is null) return 0; try { return BitConverter.ToInt32(Convert.FromBase64String(cursor)); } catch { return null; } }
    private static IResult Validation(HttpContext context, IDictionary<string, string[]> errors) =>
        HouseholdEndpoints.ValidationFailed(context, errors);
    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(ApiProblems.Create(context, status, code, title));
}
