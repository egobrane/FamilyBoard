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
        group.MapGet("/point-addition-definitions", ListAdditionDefinitionsAsync);
        group.MapPost("/point-addition-definitions", CreateAdditionDefinitionAsync)
            .RequireFamilyDashboardAntiforgery();
        group.MapPatch("/point-addition-definitions/{definitionId:guid}",
            UpdateAdditionDefinitionAsync).RequireFamilyDashboardAntiforgery();
        group.MapPost("/point-addition-definitions/{definitionId:guid}/activate",
            (Guid householdId, Guid definitionId,
                ChangePointAdditionDefinitionStateRequest? request, HttpContext context,
                IAuthorizationService authorization, HouseholdService households,
                PointService points, CancellationToken cancellationToken) =>
                ChangeAdditionDefinitionStateAsync(householdId, definitionId, true, request,
                    context, authorization, households, points, cancellationToken))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/point-addition-definitions/{definitionId:guid}/deactivate",
            (Guid householdId, Guid definitionId,
                ChangePointAdditionDefinitionStateRequest? request, HttpContext context,
                IAuthorizationService authorization, HouseholdService households,
                PointService points, CancellationToken cancellationToken) =>
                ChangeAdditionDefinitionStateAsync(householdId, definitionId, false, request,
                    context, authorization, households, points, cancellationToken))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/point-addition-definitions/{definitionId:guid}/applications",
            ApplyAdditionAsync).RequireFamilyDashboardAntiforgery();
        group.MapGet("/point-deduction-definitions", ListDeductionDefinitionsAsync);
        group.MapPost("/point-deduction-definitions", CreateDeductionDefinitionAsync)
            .RequireFamilyDashboardAntiforgery();
        group.MapPatch("/point-deduction-definitions/{definitionId:guid}",
            UpdateDeductionDefinitionAsync).RequireFamilyDashboardAntiforgery();
        group.MapPost("/point-deduction-definitions/{definitionId:guid}/activate",
            (Guid householdId, Guid definitionId,
                ChangePointDeductionDefinitionStateRequest? request, HttpContext context,
                IAuthorizationService authorization, HouseholdService households,
                PointService points, CancellationToken cancellationToken) =>
                ChangeDeductionDefinitionStateAsync(householdId, definitionId, true, request,
                    context, authorization, households, points, cancellationToken))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/point-deduction-definitions/{definitionId:guid}/deactivate",
            (Guid householdId, Guid definitionId,
                ChangePointDeductionDefinitionStateRequest? request, HttpContext context,
                IAuthorizationService authorization, HouseholdService households,
                PointService points, CancellationToken cancellationToken) =>
                ChangeDeductionDefinitionStateAsync(householdId, definitionId, false, request,
                    context, authorization, households, points, cancellationToken))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/point-deduction-definitions/{definitionId:guid}/applications",
            ApplyDeductionAsync).RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> ListAdditionDefinitionsAsync(Guid householdId,
        HttpContext context, IAuthorizationService authorization, HouseholdService households,
        PointService points, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        return failure ?? Results.Ok(await points.ListAdditionDefinitionsAsync(
            householdId, cancellationToken));
    }

    private static async Task<IResult> CreateAdditionDefinitionAsync(Guid householdId,
        CreatePointAdditionDefinitionRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null)
            return Validation(context, new Dictionary<string, string[]>
                { ["additionDefinition"] = ["Addition definition data is required."] });
        if (!PointValidation.TryAdditionDefinition(request.ClientRequestId,
                request.Title, request.AdditionAmount, out var clean, out var errors))
            return Validation(context, errors);
        return Result(context, await points.CreateAdditionDefinitionAsync(householdId, actor,
            clean!, cancellationToken), true);
    }

    private static async Task<IResult> UpdateAdditionDefinitionAsync(Guid householdId,
        Guid definitionId, UpdatePointAdditionDefinitionRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]>
                { ["additionDefinition"] = ["Addition definition data and a valid version are required."] });
        if (!PointValidation.TryAdditionDefinition(Guid.NewGuid(), request.Title,
                request.AdditionAmount, out var clean, out var errors))
            return Validation(context, errors);
        var update = new UpdatePointAdditionDefinitionRequest(request.ExpectedVersion,
            clean!.Title, clean.AdditionAmount);
        return Result(context, await points.UpdateAdditionDefinitionAsync(householdId,
            definitionId, actor, update, cancellationToken), false);
    }

    private static async Task<IResult> ChangeAdditionDefinitionStateAsync(Guid householdId,
        Guid definitionId, bool active, ChangePointAdditionDefinitionStateRequest? request,
        HttpContext context, IAuthorizationService authorization, HouseholdService households,
        PointService points, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]>
                { ["expectedVersion"] = ["A valid version is required."] });
        return Result(context, await points.SetAdditionDefinitionStateAsync(householdId,
            definitionId, actor, request.ExpectedVersion, active, cancellationToken), false);
    }

    private static async Task<IResult> ApplyAdditionAsync(Guid householdId, Guid definitionId,
        ApplyPointAdditionRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (!PointValidation.TryAdditionApplication(request, out var errors))
            return Validation(context, errors);
        return Result(context, await points.ApplyAdditionAsync(householdId, definitionId,
            actor, request!, cancellationToken), true);
    }

    private static async Task<IResult> ListDeductionDefinitionsAsync(Guid householdId,
        HttpContext context, IAuthorizationService authorization, HouseholdService households,
        PointService points, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        return failure ?? Results.Ok(await points.ListDeductionDefinitionsAsync(
            householdId, cancellationToken));
    }

    private static async Task<IResult> CreateDeductionDefinitionAsync(Guid householdId,
        CreatePointDeductionDefinitionRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null)
            return Validation(context, new Dictionary<string, string[]>
                { ["deductionDefinition"] = ["Deduction definition data is required."] });
        if (!PointValidation.TryDeductionDefinition(request.ClientRequestId,
                request.Title, request.DeductionAmount, out var clean, out var errors))
            return Validation(context, errors);
        return Result(context, await points.CreateDeductionDefinitionAsync(householdId, actor,
            clean!, cancellationToken), true);
    }

    private static async Task<IResult> UpdateDeductionDefinitionAsync(Guid householdId,
        Guid definitionId, UpdatePointDeductionDefinitionRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]>
                { ["deductionDefinition"] = ["Deduction definition data and a valid version are required."] });
        if (!PointValidation.TryDeductionDefinition(Guid.NewGuid(), request.Title,
                request.DeductionAmount, out var clean, out var errors))
            return Validation(context, errors);
        var update = new UpdatePointDeductionDefinitionRequest(request.ExpectedVersion,
            clean!.Title, clean.DeductionAmount);
        return Result(context, await points.UpdateDeductionDefinitionAsync(householdId,
            definitionId, actor, update, cancellationToken), false);
    }

    private static async Task<IResult> ChangeDeductionDefinitionStateAsync(Guid householdId,
        Guid definitionId, bool active, ChangePointDeductionDefinitionStateRequest? request,
        HttpContext context, IAuthorizationService authorization, HouseholdService households,
        PointService points, CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]>
                { ["expectedVersion"] = ["A valid version is required."] });
        return Result(context, await points.SetDeductionDefinitionStateAsync(householdId,
            definitionId, actor, request.ExpectedVersion, active, cancellationToken), false);
    }

    private static async Task<IResult> ApplyDeductionAsync(Guid householdId, Guid definitionId,
        ApplyPointDeductionRequest? request, HttpContext context,
        IAuthorizationService authorization, HouseholdService households, PointService points,
        CancellationToken cancellationToken)
    {
        var failure = await AuthorizeAsync(householdId, true, context, authorization,
            households, cancellationToken);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor))
            return HouseholdEndpoints.AccountUnavailable(context);
        if (!PointValidation.TryDeductionApplication(request, out var errors))
            return Validation(context, errors);
        return Result(context, await points.ApplyDeductionAsync(householdId, definitionId,
            actor, request!, cancellationToken), true);
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

    private static IResult Result<T>(HttpContext context,
        PointOperationResult<T> result, bool created) => result.Status switch
    {
        PointOperationStatus.Success when created => Results.Json(result.Value,
            statusCode: StatusCodes.Status201Created),
        PointOperationStatus.Success => Results.Ok(result.Value),
        PointOperationStatus.MemberNotFound => Problem(context, 404,
            ApiProblemCodes.PointMemberNotFound, "The household member was not found."),
        PointOperationStatus.MemberInactive => Problem(context, 409,
            ApiProblemCodes.PointMemberInactive, "Select an active household member."),
        PointOperationStatus.AdditionDefinitionNotFound => Problem(context, 404,
            ApiProblemCodes.PointAdditionDefinitionNotFound, "The point addition was not found."),
        PointOperationStatus.AdditionDefinitionInactive => Problem(context, 409,
            ApiProblemCodes.PointAdditionDefinitionInactive, "That point addition is inactive."),
        PointOperationStatus.DeductionDefinitionNotFound => Problem(context, 404,
            ApiProblemCodes.PointDeductionDefinitionNotFound, "The point deduction was not found."),
        PointOperationStatus.DeductionDefinitionInactive => Problem(context, 409,
            ApiProblemCodes.PointDeductionDefinitionInactive, "That point deduction is inactive."),
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
