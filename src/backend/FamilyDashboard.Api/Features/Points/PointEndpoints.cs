using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace FamilyDashboard.Api.Features.Points;

public static class PointEndpoints
{
    public static IEndpointRouteBuilder MapPointEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/households/{householdId:guid}").RequireAuthorization();
        group.MapGet("/points/summary", GetSummaryAsync);
        group.MapGet("/point-transactions", ListAsync);
        group.MapPost("/point-adjustments", AdjustAsync).RequireFamilyDashboardAntiforgery();
        group.MapPost("/point-transactions/{transactionId:guid}/reverse", ReverseAsync)
            .RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> GetSummaryAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, false, context, authorization,
            households, cancellationToken);
        return failure ?? Results.Ok(await points.GetSummaryAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> ListAsync(Guid householdId, Guid? memberId, string? cursor,
        int? pageSize, HttpContext context, IAuthorizationService authorization,
        HouseholdService households, PointService points, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, false, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        var offset = DecodeCursor(cursor);
        if (offset is null) return Validation(context,
            new Dictionary<string, string[]> { ["cursor"] = ["The cursor is invalid."] });
        return Results.Ok(await points.ListAsync(householdId, memberId, offset.Value,
            Math.Clamp(pageSize ?? 50, 1, 100), cancellationToken));
    }

    private static async Task<IResult> AdjustAsync(Guid householdId,
        CreatePointAdjustmentRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (!PointValidation.TryAdjustment(request, out var clean, out var errors))
            return Validation(context, errors);
        return Result(context, await points.AdjustAsync(householdId, actor, clean!, cancellationToken), true);
    }

    private static async Task<IResult> ReverseAsync(Guid householdId, Guid transactionId,
        ReversePointTransactionRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (!PointValidation.TryReversal(request, out var clean, out var errors))
            return Validation(context, errors);
        return Result(context, await points.ReverseAsync(householdId, transactionId, actor,
            clean!, cancellationToken), true);
    }

    private static async Task<IResult?> AuthorizeAsync(Guid householdId, bool administration,
        HttpContext context, IAuthorizationService authorization, HouseholdService households,
        CancellationToken cancellationToken)
    {
        if (await HouseholdEndpoints.ResolveAccountAsync(context, households, cancellationToken) is null)
            return HouseholdEndpoints.AccountUnavailable(context);
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, householdId,
                HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        if (!administration) return null;
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, householdId,
                HouseholdAuthorizationPolicies.Adult, cancellationToken))
            return HouseholdEndpoints.AdultAccessRequired(context);
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, householdId,
                HouseholdAuthorizationPolicies.Administration, cancellationToken))
            return ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context);
        return null;
    }

    private static IResult Result(HttpContext context,
        PointOperationResult<PointTransactionResponse> result, bool created) => result.Status switch
    {
        PointOperationStatus.Success when created => Results.Json(result.Value,
            statusCode: StatusCodes.Status201Created),
        PointOperationStatus.Success => Results.Ok(result.Value),
        PointOperationStatus.MemberNotFound => Problem(context, 404,
            ApiProblemCodes.PointMemberNotFound, "The household member was not found."),
        PointOperationStatus.TransactionNotFound => Problem(context, 404,
            ApiProblemCodes.PointTransactionNotFound, "The point transaction was not found."),
        PointOperationStatus.IdempotencyConflict => Problem(context, 409,
            ApiProblemCodes.PointIdempotencyConflict, "That request ID was already used for different point data."),
        PointOperationStatus.AlreadyReversed => Problem(context, 409,
            ApiProblemCodes.PointTransactionAlreadyReversed, "That point transaction has already been reversed."),
        PointOperationStatus.NotReversible => Problem(context, 409,
            ApiProblemCodes.PointTransactionNotReversible, "That point transaction cannot be reversed."),
        _ => Problem(context, 409, ApiProblemCodes.PointConcurrencyConflict,
            "Point information changed while the request was being processed. Refresh and try again."),
    };

    private static int? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return 0;
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            return bytes.Length == sizeof(int) ? BitConverter.ToInt32(bytes) : null;
        }
        catch (FormatException) { return null; }
    }

    private static IResult Validation(HttpContext context, IDictionary<string, string[]> errors) =>
        Results.Problem(ApiProblems.Create(context, 400, ApiProblemCodes.ValidationFailed,
            "Some point information needs attention.", errors: errors));

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(ApiProblems.Create(context, status, code, title));
}
