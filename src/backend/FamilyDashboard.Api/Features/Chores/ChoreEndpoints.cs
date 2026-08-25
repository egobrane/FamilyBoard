using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace FamilyDashboard.Api.Features.Chores;

public static class ChoreEndpoints
{
    public static IEndpointRouteBuilder MapChoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/households/{householdId:guid}").RequireAuthorization();
        group.MapGet("/chores/participants", ListParticipantsAsync);
        group.MapGet("/chores/dashboard", GetDashboardAsync);
        group.MapGet("/chore-assignments", ListAssignmentsAsync);
        group.MapPost("/chore-assignments/{assignmentId:guid}/completions", CompleteAsync)
            .RequireFamilyDashboardAntiforgery().RequireRateLimiting("chore-completion");

        group.MapGet("/chore-definitions", ListDefinitionsAsync);
        group.MapPost("/chore-definitions", CreateDefinitionAsync).RequireFamilyDashboardAntiforgery();
        group.MapPatch("/chore-definitions/{definitionId:guid}", UpdateDefinitionAsync).RequireFamilyDashboardAntiforgery();
        group.MapPost("/chore-definitions/{definitionId:guid}/activate",
            (Guid householdId, Guid definitionId, ChangeChoreDefinitionStateRequest? request, HttpContext context,
                IAuthorizationService auth, HouseholdService households, ChoreService chores, CancellationToken token) =>
                ChangeDefinitionStateAsync(householdId, definitionId, true, request, context, auth, households, chores, token))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/chore-definitions/{definitionId:guid}/deactivate",
            (Guid householdId, Guid definitionId, ChangeChoreDefinitionStateRequest? request, HttpContext context,
                IAuthorizationService auth, HouseholdService households, ChoreService chores, CancellationToken token) =>
                ChangeDefinitionStateAsync(householdId, definitionId, false, request, context, auth, households, chores, token))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/chore-assignments", CreateAssignmentAsync).RequireFamilyDashboardAntiforgery();
        group.MapPost("/chore-assignments/{assignmentId:guid}/skip", SkipAsync).RequireFamilyDashboardAntiforgery();
        group.MapGet("/chore-completions", ListPendingReviewsAsync);
        group.MapPost("/chore-completions/{completionId:guid}/review", ReviewAsync).RequireFamilyDashboardAntiforgery();
        group.MapGet("/chore-schedules", ListSchedulesAsync);
        group.MapGet("/chore-schedules/{scheduleId:guid}", GetScheduleAsync);
        group.MapPost("/chore-schedules", CreateScheduleAsync).RequireFamilyDashboardAntiforgery();
        group.MapPatch("/chore-schedules/{scheduleId:guid}", UpdateScheduleAsync).RequireFamilyDashboardAntiforgery();
        group.MapPost("/chore-schedules/{scheduleId:guid}/pause",
            (Guid householdId, Guid scheduleId, ChangeChoreScheduleStateRequest? request,
                HttpContext context, IAuthorizationService auth, HouseholdService households,
                ChoreScheduleService schedules, CancellationToken token) => ChangeScheduleStateAsync(
                    householdId, scheduleId, false, request, context, auth, households, schedules, token))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/chore-schedules/{scheduleId:guid}/resume",
            (Guid householdId, Guid scheduleId, ChangeChoreScheduleStateRequest? request,
                HttpContext context, IAuthorizationService auth, HouseholdService households,
                ChoreScheduleService schedules, CancellationToken token) => ChangeScheduleStateAsync(
                    householdId, scheduleId, true, request, context, auth, households, schedules, token))
            .RequireFamilyDashboardAntiforgery();
        group.MapPost("/chore-schedules/preview", PreviewScheduleAsync).RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> ListSchedulesAsync(Guid householdId, bool? includeInactive,
        HttpContext context, IAuthorizationService auth, HouseholdService households,
        ChoreScheduleService schedules, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        return failure ?? Results.Ok(await schedules.ListAsync(householdId, includeInactive == true, token));
    }

    private static async Task<IResult> GetScheduleAsync(Guid householdId, Guid scheduleId,
        HttpContext context, IAuthorizationService auth, HouseholdService households,
        ChoreScheduleService schedules, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        return failure ?? ScheduleResult(context, await schedules.GetAsync(householdId, scheduleId, token));
    }

    private static async Task<IResult> CreateScheduleAsync(Guid householdId,
        CreateChoreScheduleRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, ChoreScheduleService schedules, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || request is null
            || request.ClientRequestId == Guid.Empty || request.ChoreDefinitionId == Guid.Empty
            || request.AssignedMemberId == Guid.Empty)
            return Validation(context, new Dictionary<string, string[]> { ["schedule"] = ["Definition, member, recurrence, start date, and request ID are required."] });
        if (!ChoreValidation.TrySchedule(request.Recurrence, request.StartLocalDate,
                request.EndLocalDate, out var recurrence, out var errors)) return Validation(context, errors);
        return ScheduleResult(context, await schedules.CreateAsync(householdId, actor, request,
            recurrence, token), true);
    }

    private static async Task<IResult> UpdateScheduleAsync(Guid householdId, Guid scheduleId,
        UpdateChoreScheduleRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, ChoreScheduleService schedules, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (request is null || request.ExpectedVersion < 1 || request.ChoreDefinitionId == Guid.Empty
            || request.AssignedMemberId == Guid.Empty)
            return Validation(context, new Dictionary<string, string[]> { ["schedule"] = ["Definition, member, recurrence, start date, and version are required."] });
        if (!ChoreValidation.TrySchedule(request.Recurrence, request.StartLocalDate,
                request.EndLocalDate, out var recurrence, out var errors)) return Validation(context, errors);
        return ScheduleResult(context, await schedules.UpdateAsync(householdId, scheduleId,
            request, recurrence, token));
    }

    private static async Task<IResult> ChangeScheduleStateAsync(Guid householdId, Guid scheduleId,
        bool active, ChangeChoreScheduleStateRequest? request, HttpContext context,
        IAuthorizationService auth, HouseholdService households, ChoreScheduleService schedules,
        CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]> { ["expectedVersion"] = ["A valid version is required."] });
        return ScheduleResult(context, await schedules.SetStateAsync(householdId, scheduleId,
            request.ExpectedVersion, active, token));
    }

    private static async Task<IResult> PreviewScheduleAsync(Guid householdId,
        PreviewChoreScheduleRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, ChoreScheduleService schedules, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (request is null)
            return Validation(context, new Dictionary<string, string[]> { ["schedule"] = ["Schedule data is required."] });
        if (!ChoreValidation.TrySchedule(request.Recurrence, request.StartLocalDate,
                request.EndLocalDate, out var recurrence, out var errors)) return Validation(context, errors);
        return Results.Ok(schedules.Preview(request, recurrence));
    }

    private static async Task<IResult> ListParticipantsAsync(Guid householdId, HttpContext context,
        IAuthorizationService auth, HouseholdService households, ChoreService chores, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, false, context, auth, households, token);
        return failure ?? Results.Ok(await chores.ListParticipantsAsync(householdId, token));
    }

    private static async Task<IResult> GetDashboardAsync(Guid householdId, HttpContext context,
        IAuthorizationService auth, HouseholdService households, ChoreService chores, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, false, context, auth, households, token);
        return failure ?? Results.Ok(await chores.GetDashboardAsync(householdId, token));
    }

    private static async Task<IResult> ListAssignmentsAsync(Guid householdId, string? view, Guid? memberId,
        string? cursor, int? pageSize, HttpContext context, IAuthorizationService auth,
        HouseholdService households, ChoreService chores, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, false, context, auth, households, token);
        if (failure is not null) return failure;
        var offset = DecodeCursor(cursor);
        if (offset is null) return Validation(context, new Dictionary<string, string[]> { ["cursor"] = ["The cursor is invalid."] });
        return Results.Ok(await chores.ListAssignmentsAsync(householdId, view ?? "active", memberId,
            offset.Value, Math.Clamp(pageSize ?? 50, 1, 100), token));
    }

    private static async Task<IResult> ListDefinitionsAsync(Guid householdId, bool? includeInactive,
        HttpContext context, IAuthorizationService auth, HouseholdService households, ChoreService chores,
        CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        return failure ?? Results.Ok(await chores.ListDefinitionsAsync(householdId, includeInactive == true, token));
    }

    private static async Task<IResult> CreateDefinitionAsync(Guid householdId, CreateChoreDefinitionRequest? request,
        HttpContext context, IAuthorizationService auth, HouseholdService households, ChoreService chores,
        CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (request is null) return Validation(context,
            new Dictionary<string, string[]> { ["definition"] = ["Definition data is required."] });
        if (!ChoreValidation.TryDefinition(request.ClientRequestId, request.Title,
                request.Description, request.DefaultPointValue, out var values, out var errors)) return Validation(context, errors);
        var result = await chores.CreateDefinitionAsync(householdId, values, token);
        return Result(context, result, "definition", true);
    }

    private static async Task<IResult> UpdateDefinitionAsync(Guid householdId, Guid definitionId,
        UpdateChoreDefinitionRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, ChoreService chores, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (request is null || request.ExpectedVersion < 1) return Validation(context,
            new Dictionary<string, string[]> { ["expectedVersion"] = ["Definition data and a valid version are required."] });
        if (!ChoreValidation.TryDefinition(Guid.NewGuid(), request.Title, request.Description,
                request.DefaultPointValue,
                out var values, out var errors)) return Validation(context, errors);
        return Result(context, await chores.UpdateDefinitionAsync(householdId, definitionId,
            request.ExpectedVersion, values.Title, values.Description, values.DefaultPointValue, null, token), "definition");
    }

    private static async Task<IResult> ChangeDefinitionStateAsync(Guid householdId, Guid definitionId, bool active,
        ChangeChoreDefinitionStateRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, ChoreService chores, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]> { ["expectedVersion"] = ["A valid version is required."] });
        return Result(context, await chores.SetDefinitionStateAsync(householdId, definitionId,
            request.ExpectedVersion, active, token), "definition");
    }

    private static async Task<IResult> CreateAssignmentAsync(Guid householdId, CreateChoreAssignmentRequest? request,
        HttpContext context, IAuthorizationService auth, HouseholdService households, ChoreService chores,
        CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || request is null
            || request.ClientRequestId == Guid.Empty || request.ChoreDefinitionId == Guid.Empty
            || request.AssignedMemberId == Guid.Empty)
            return Validation(context, new Dictionary<string, string[]> { ["assignment"] = ["Definition, member, due date, and request ID are required."] });
        return Result(context, await chores.CreateAssignmentAsync(householdId, actor, request, token), "assignment", true);
    }

    private static async Task<IResult> CompleteAsync(Guid householdId, Guid assignmentId, CompleteChoreRequest? request,
        HttpContext context, IAuthorizationService auth, HouseholdService households, ChoreService chores,
        CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, false, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || !context.User.TryGetUserSessionId(out var session)
            || request is null || request.ClientRequestId == Guid.Empty || request.ExpectedAssignmentVersion < 1)
            return Validation(context, new Dictionary<string, string[]> { ["completion"] = ["A valid request ID and assignment version are required."] });
        return Result(context, await chores.CompleteAsync(householdId, assignmentId, actor, session, request, token), "assignment");
    }

    private static async Task<IResult> SkipAsync(Guid householdId, Guid assignmentId, SkipChoreAssignmentRequest? request,
        HttpContext context, IAuthorizationService auth, HouseholdService households, ChoreService chores,
        CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || request is null || request.ExpectedVersion < 1)
            return Validation(context, new Dictionary<string, string[]> { ["expectedVersion"] = ["A valid version is required."] });
        if (!ChoreValidation.TryNote(request.Reason, "reason", out var reason, out var errors))
            return Validation(context, errors);
        return Result(context, await chores.SkipAsync(householdId, assignmentId, actor,
            request with { Reason = reason }, token), "assignment");
    }

    private static async Task<IResult> ListPendingReviewsAsync(Guid householdId, string? status,
        HttpContext context, IAuthorizationService auth, HouseholdService households, ChoreService chores,
        CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (status is not null && !status.Equals("pendingReview", StringComparison.OrdinalIgnoreCase))
            return Validation(context, new Dictionary<string, string[]> { ["status"] = ["Only pendingReview is supported."] });
        return Results.Ok(await chores.ListPendingReviewsAsync(householdId, token));
    }

    private static async Task<IResult> ReviewAsync(Guid householdId, Guid completionId,
        ReviewChoreCompletionRequest? request, HttpContext context, IAuthorizationService auth,
        HouseholdService households, ChoreService chores, CancellationToken token)
    {
        var failure = await AuthorizeAsync(householdId, true, context, auth, households, token);
        if (failure is not null) return failure;
        if (!context.User.TryGetUserAccountId(out var actor) || request is null || request.ExpectedVersion < 1
            || (request.Decision != "approved" && request.Decision != "rejected"))
            return Validation(context, new Dictionary<string, string[]> { ["decision"] = ["Choose approved or rejected and provide a valid version."] });
        if (!ChoreValidation.TryNote(request.Note, "note", out var note, out var errors))
            return Validation(context, errors);
        return Result(context, await chores.ReviewAsync(householdId, completionId, actor,
            request with { Note = note }, token), "completion");
    }

    private static async Task<IResult?> AuthorizeAsync(Guid householdId, bool administration,
        HttpContext context, IAuthorizationService authorizationService, HouseholdService householdService,
        CancellationToken cancellationToken)
    {
        if (await HouseholdEndpoints.ResolveAccountAsync(context, householdService, cancellationToken) is null)
            return HouseholdEndpoints.AccountUnavailable(context);
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorizationService, householdId,
                HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        if (!administration) return null;
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorizationService, householdId,
                HouseholdAuthorizationPolicies.Adult, cancellationToken))
            return HouseholdEndpoints.AdultAccessRequired(context);
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorizationService, householdId,
                HouseholdAuthorizationPolicies.Administration, cancellationToken))
            return ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context);
        return null;
    }

    private static IResult Result<T>(HttpContext context, ChoreOperationResult<T> result,
        string resource, bool created = false) => result.Status switch
    {
        ChoreOperationStatus.Success when created => Results.Json(result.Value, statusCode: StatusCodes.Status201Created),
        ChoreOperationStatus.Success => Results.Ok(result.Value),
        ChoreOperationStatus.NotFound => Problem(context, 404,
            resource == "definition" ? ApiProblemCodes.ChoreDefinitionNotFound
                : resource == "completion" ? ApiProblemCodes.ChoreCompletionNotFound
                : ApiProblemCodes.ChoreAssignmentNotFound, "The chore resource was not found."),
        ChoreOperationStatus.DefinitionInactive => Problem(context, 409, ApiProblemCodes.ChoreDefinitionInactive,
            "The chore definition is inactive."),
        ChoreOperationStatus.MemberInactive => Problem(context, 409, ApiProblemCodes.ChoreMemberInactive,
            "Select an active household member."),
        ChoreOperationStatus.NotActionable => Problem(context, 409, ApiProblemCodes.ChoreAssignmentNotActionable,
            "The assignment cannot be changed in its current state."),
        ChoreOperationStatus.PendingReview => Problem(context, 409, ApiProblemCodes.ChoreCompletionPendingReview,
            "This assignment is already waiting for adult review."),
        ChoreOperationStatus.AlreadyReviewed => Problem(context, 409, ApiProblemCodes.ChoreCompletionAlreadyReviewed,
            "The completion has already received a different review decision."),
        ChoreOperationStatus.IdempotencyConflict => Problem(context, 409, ApiProblemCodes.ChoreIdempotencyConflict,
            "The request ID was already used for different chore data."),
        ChoreOperationStatus.ConcurrencyConflict => Problem(context, 409, ApiProblemCodes.ChoreConcurrencyConflict,
            "The chore changed concurrently. Refresh and try again."),
        ChoreOperationStatus.InvalidDueDate => Validation(context,
            new Dictionary<string, string[]> { ["dueLocalDate"] = ["The due date or time is invalid for this household."] }),
        _ => throw new InvalidOperationException("Unsupported chore result."),
    };

    private static IResult ScheduleResult<T>(HttpContext context,
        ChoreScheduleOperationResult<T> result, bool created = false) => result.Status switch
    {
        ChoreScheduleOperationStatus.Success when created => Results.Json(result.Value, statusCode: StatusCodes.Status201Created),
        ChoreScheduleOperationStatus.Success => Results.Ok(result.Value),
        ChoreScheduleOperationStatus.NotFound => Problem(context, 404, ApiProblemCodes.ChoreScheduleNotFound,
            "The chore schedule was not found."),
        ChoreScheduleOperationStatus.DefinitionInactive => Problem(context, 409, ApiProblemCodes.ChoreDefinitionInactive,
            "The chore definition is inactive."),
        ChoreScheduleOperationStatus.MemberInactive => Problem(context, 409, ApiProblemCodes.ChoreMemberInactive,
            "Select an active household member."),
        ChoreScheduleOperationStatus.IdempotencyConflict => Problem(context, 409, ApiProblemCodes.ChoreScheduleRequestConflict,
            "The request ID was already used for different schedule data."),
        ChoreScheduleOperationStatus.ConcurrencyConflict => Problem(context, 409, ApiProblemCodes.ChoreScheduleVersionConflict,
            "The chore schedule changed concurrently. Refresh and try again."),
        ChoreScheduleOperationStatus.DependencyInactive => Problem(context, 409, ApiProblemCodes.ChoreScheduleDependencyInactive,
            "Reactivate the chore definition and assigned member before resuming this schedule."),
        ChoreScheduleOperationStatus.InvalidSchedule => Problem(context, 400, ApiProblemCodes.ChoreScheduleInvalid,
            "The chore schedule is not valid for this household."),
        _ => throw new InvalidOperationException("Unsupported chore schedule result."),
    };

    private static IResult Validation(HttpContext context, IDictionary<string, string[]> errors) =>
        HouseholdEndpoints.ValidationFailed(context, errors);
    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(ApiProblems.Create(context, status, code, title));

    private static int? DecodeCursor(string? cursor)
    {
        if (cursor is null) return 0;
        try { return BitConverter.ToInt32(Convert.FromBase64String(cursor)); }
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
