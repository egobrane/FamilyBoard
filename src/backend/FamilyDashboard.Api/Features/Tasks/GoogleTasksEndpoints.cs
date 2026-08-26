using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Tasks;

public static class GoogleTasksEndpoints
{
    public static IEndpointRouteBuilder MapGoogleTasksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/households/{householdId:guid}/tasks/connection", GetConnectionAsync).RequireAuthorization();
        endpoints.MapPost("/api/households/{householdId:guid}/tasks/authorization", BeginAuthorizationAsync).RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/integrations/google-tasks/callback", CallbackAsync).RequireAuthorization();
        endpoints.MapGet("/api/households/{householdId:guid}/tasks/provider-task-lists", ListProviderTaskListsAsync).RequireAuthorization();
        endpoints.MapGet("/api/households/{householdId:guid}/tasks/sources", ListSourcesAsync).RequireAuthorization();
        endpoints.MapPut("/api/households/{householdId:guid}/tasks/sources", UpdateSourcesAsync).RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapPost("/api/households/{householdId:guid}/tasks/disconnect", DisconnectAsync).RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/households/{householdId:guid}/tasks", ListTasksAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetConnectionAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, GoogleTasksService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization, HouseholdAuthorizationPolicies.Administration, cancellationToken);
            return access.Result ?? Results.Ok(await service.GetConnectionAsync(householdId, access.UserAccountId!.Value, cancellationToken));
        });

    private static async Task<IResult> BeginAuthorizationAsync(Guid householdId,
        BeginTasksAuthorizationRequest? request, HttpContext context, IAuthorizationService authorization,
        GoogleTasksService service, TasksCorrelationCookieService correlationCookie,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization, HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (access.Result is not null) return access.Result;
            if (!context.User.TryGetUserSessionId(out var sessionId)) return HouseholdEndpoints.AccountUnavailable(context);
            if (!ReturnUrlValidator.TryNormalize(request?.ReturnPath, out var returnPath))
                return Problem(context, 400, ApiProblemCodes.InvalidReturnUrl, "The return path is invalid.");
            var result = service.BeginAuthorization(householdId, access.UserAccountId!.Value, sessionId, returnPath);
            correlationCookie.Write(context.Response, result.State, result.Response.ExpiresAt);
            return Results.Ok(result.Response);
        });

    private static async Task<IResult> CallbackAsync(string? code, string? state, string? error,
        HttpContext context, IAuthorizationService authorization, TasksStateProtector stateProtector,
        TasksCorrelationCookieService correlationCookie, GoogleTasksService service,
        IOptions<AuthenticationConfiguration> authentication, CancellationToken cancellationToken)
    {
        var origin = authentication.Value.FrontendOrigin.TrimEnd('/');
        var correlationValid = state is not null && correlationCookie.ValidateAndDelete(context.Request, context.Response, state);
        if (!string.IsNullOrWhiteSpace(error)) return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.TasksAuthorizationDenied}");
        if (string.IsNullOrWhiteSpace(code) || !stateProtector.TryReadAuthorization(state, out var payload)
            || !correlationValid || !context.User.TryGetUserAccountId(out var userAccountId)
            || !context.User.TryGetUserSessionId(out var sessionId) || payload!.UserAccountId != userAccountId
            || payload.UserSessionId != sessionId)
            return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.TasksAuthorizationExpired}");
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, payload.HouseholdId,
                HouseholdAuthorizationPolicies.Administration, cancellationToken))
            return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.ParentElevationRequired}");
        try
        {
            var result = await service.CompleteAuthorizationAsync(code, state!, userAccountId, sessionId, cancellationToken);
            var separator = result.ReturnPath.Contains('?') ? '&' : '?';
            return Results.Redirect($"{origin}{result.ReturnPath}{separator}tasks=connected");
        }
        catch (TasksOperationException exception) { return Results.Redirect($"{origin}/auth/error?code={Uri.EscapeDataString(exception.Code)}"); }
        catch (GoogleTasksProviderException) { return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.TasksAuthorizationFailed}"); }
    }

    private static async Task<IResult> ListProviderTaskListsAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, GoogleTasksService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization, HouseholdAuthorizationPolicies.Administration, cancellationToken);
            return access.Result ?? Results.Ok(await service.ListProviderTaskListsAsync(householdId, access.UserAccountId!.Value, cancellationToken));
        });

    private static async Task<IResult> ListSourcesAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, GoogleTasksService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization, HouseholdAuthorizationPolicies.Administration, cancellationToken);
            return access.Result ?? Results.Ok(await service.ListSourcesAsync(householdId, access.UserAccountId!.Value, cancellationToken));
        });

    private static async Task<IResult> UpdateSourcesAsync(Guid householdId, UpdateTaskListSourcesRequest? request,
        HttpContext context, IAuthorizationService authorization, GoogleTasksService service,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization, HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (access.Result is not null) return access.Result;
            if (request is null || request.ConnectionId == Guid.Empty)
                return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["connectionId"] = ["A Google Tasks connection is required."] });
            return Results.Ok(await service.UpdateSourcesAsync(householdId, access.UserAccountId!.Value, request, cancellationToken));
        });

    private static async Task<IResult> DisconnectAsync(Guid householdId, DisconnectTasksRequest? request,
        HttpContext context, IAuthorizationService authorization, GoogleTasksService service,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization, HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (access.Result is not null) return access.Result;
            if (request is null || request.ConnectionId == Guid.Empty)
                return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["connectionId"] = ["A Google Tasks connection is required."] });
            await service.DisconnectAsync(access.UserAccountId!.Value, request, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> ListTasksAsync(Guid householdId, bool? includeCompleted, string? cursor,
        HttpContext context, IAuthorizationService authorization, GoogleTasksService service,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization, HouseholdAuthorizationPolicies.Member, cancellationToken);
            return access.Result ?? Results.Ok(await service.ListTasksAsync(householdId, includeCompleted ?? false, cursor, cancellationToken));
        });

    private static async Task<(Guid? UserAccountId, IResult? Result)> RequireAccessAsync(Guid householdId,
        HttpContext context, IAuthorizationService authorization, string policy, CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId)) return (null, HouseholdEndpoints.AccountUnavailable(context));
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, householdId, HouseholdAuthorizationPolicies.Member, cancellationToken))
            return (null, HouseholdEndpoints.HouseholdNotFound(context));
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, householdId, policy, cancellationToken))
            return (null, policy == HouseholdAuthorizationPolicies.Administration
                ? ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context) : HouseholdEndpoints.AdultAccessRequired(context));
        return (userAccountId, null);
    }

    private static async Task<IResult> RunAsync(HttpContext context, Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (TasksOperationException exception) { return Problem(context, exception.Status, exception.Code, exception.Message); }
        catch (GoogleTasksProviderException exception)
        {
            var (status, code) = exception.Failure == GoogleTasksProviderFailure.RateLimited
                ? (429, ApiProblemCodes.TasksProviderRateLimited) : (503, ApiProblemCodes.TasksProviderUnavailable);
            return Problem(context, status, code, "Google Tasks is temporarily unavailable.");
        }
    }

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(ApiProblems.Create(context, status, code, title));
}
