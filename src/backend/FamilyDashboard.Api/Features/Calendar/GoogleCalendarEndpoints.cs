using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Calendar;

public static class GoogleCalendarEndpoints
{
    public static IEndpointRouteBuilder MapGoogleCalendarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/households/{householdId:guid}/calendar/connection", GetConnectionAsync)
            .RequireAuthorization();
        endpoints.MapPost("/api/households/{householdId:guid}/calendar/authorization", BeginAuthorizationAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/integrations/google-calendar/callback", CallbackAsync)
            .RequireAuthorization();
        endpoints.MapGet("/api/households/{householdId:guid}/calendar/provider-calendars", ListProviderCalendarsAsync)
            .RequireAuthorization();
        endpoints.MapGet("/api/households/{householdId:guid}/calendar/sources", ListSourcesAsync)
            .RequireAuthorization();
        endpoints.MapPut("/api/households/{householdId:guid}/calendar/sources", UpdateSourcesAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapPost("/api/households/{householdId:guid}/calendar/disconnect", DisconnectAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/households/{householdId:guid}/calendar/event-creation-target", GetEventCreationTargetAsync)
            .RequireAuthorization();
        endpoints.MapGet("/api/households/{householdId:guid}/calendar/display-settings", GetDisplaySettingsAsync)
            .RequireAuthorization();
        endpoints.MapPut("/api/households/{householdId:guid}/calendar/event-creation-target", UpdateEventCreationTargetAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/households/{householdId:guid}/calendar/events", ListEventsAsync)
            .RequireAuthorization();
        endpoints.MapPost("/api/households/{householdId:guid}/calendar/events", CreateEventAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery()
            .RequireRateLimiting("calendar-event-creation");
        endpoints.MapGet("/api/households/{householdId:guid}/calendar/managed-events/{managementId:guid}", GetManagedEventAsync)
            .RequireAuthorization();
        endpoints.MapPut("/api/households/{householdId:guid}/calendar/managed-events/{managementId:guid}", UpdateManagedEventAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery()
            .RequireRateLimiting("calendar-event-creation");
        endpoints.MapPost("/api/households/{householdId:guid}/calendar/managed-events/{managementId:guid}/delete", DeleteManagedEventAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery()
            .RequireRateLimiting("calendar-event-creation");
        return endpoints;
    }

    private static async Task<IResult> GetConnectionAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        GoogleCalendarService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            return userId.Result ?? Results.Ok(await service.GetConnectionAsync(
                householdId, userId.UserAccountId!.Value, cancellationToken));
        });

    private static async Task<IResult> BeginAuthorizationAsync(
        Guid householdId, BeginCalendarAuthorizationRequest? request, HttpContext context,
        IAuthorizationService authorization, GoogleCalendarService service,
        CalendarCorrelationCookieService correlationCookie,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (userId.Result is not null) return userId.Result;
            if (!context.User.TryGetUserSessionId(out var sessionId))
                return HouseholdEndpoints.AccountUnavailable(context);
            if (!ReturnUrlValidator.TryNormalize(request?.ReturnPath, out var returnPath))
                return Problem(context, 400, ApiProblemCodes.InvalidReturnUrl,
                    "The return path is invalid.");
            var capability = string.IsNullOrWhiteSpace(request?.Capability)
                ? CalendarAuthorizationCapabilities.ReadOnly
                : request.Capability;
            var result = service.BeginAuthorization(
                householdId, userId.UserAccountId!.Value, sessionId, returnPath, capability);
            correlationCookie.Write(context.Response, result.State, result.Response.ExpiresAt);
            return Results.Ok(result.Response);
        });

    private static async Task<IResult> CallbackAsync(
        string? code, string? state, string? error, HttpContext context,
        IAuthorizationService authorization, CalendarStateProtector stateProtector,
        CalendarCorrelationCookieService correlationCookie,
        GoogleCalendarService service, IOptions<AuthenticationConfiguration> authentication,
        CancellationToken cancellationToken)
    {
        var origin = authentication.Value.FrontendOrigin.TrimEnd('/');
        var correlationValid = state is not null
            && correlationCookie.ValidateAndDelete(context.Request, context.Response, state);
        if (!string.IsNullOrWhiteSpace(error))
            return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.CalendarAuthorizationDenied}");
        if (string.IsNullOrWhiteSpace(code) || !stateProtector.TryReadAuthorization(state, out var payload)
            || !correlationValid
            || !context.User.TryGetUserAccountId(out var userAccountId)
            || !context.User.TryGetUserSessionId(out var sessionId)
            || payload!.UserAccountId != userAccountId
            || payload.UserSessionId != sessionId)
            return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.CalendarAuthorizationExpired}");
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, payload.HouseholdId,
                HouseholdAuthorizationPolicies.Administration, cancellationToken))
            return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.ParentElevationRequired}");
        try
        {
            var result = await service.CompleteAuthorizationAsync(
                code, state!, userAccountId, sessionId, cancellationToken);
            var separator = result.ReturnPath.Contains('?') ? '&' : '?';
            return Results.Redirect($"{origin}{result.ReturnPath}{separator}calendar=connected");
        }
        catch (CalendarOperationException exception)
        {
            return Results.Redirect($"{origin}/auth/error?code={Uri.EscapeDataString(exception.Code)}");
        }
        catch (GoogleCalendarProviderException)
        {
            return Results.Redirect($"{origin}/auth/error?code={ApiProblemCodes.CalendarAuthorizationFailed}");
        }
    }

    private static async Task<IResult> ListProviderCalendarsAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        GoogleCalendarService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            return userId.Result ?? Results.Ok(await service.ListProviderCalendarsAsync(
                householdId, userId.UserAccountId!.Value, cancellationToken));
        });

    private static async Task<IResult> ListSourcesAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        GoogleCalendarService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            return userId.Result ?? Results.Ok(await service.ListSourcesAsync(
                householdId, userId.UserAccountId!.Value, cancellationToken));
        });

    private static async Task<IResult> UpdateSourcesAsync(
        Guid householdId, UpdateCalendarSourcesRequest? request, HttpContext context,
        IAuthorizationService authorization, GoogleCalendarService service,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (userId.Result is not null) return userId.Result;
            if (request is null || request.ConnectionId == Guid.Empty)
                return HouseholdEndpoints.ValidationFailed(context,
                    new Dictionary<string, string[]> { ["connectionId"] = ["A calendar connection is required."] });
            return Results.Ok(await service.UpdateSourcesAsync(
                householdId, userId.UserAccountId!.Value, request, cancellationToken));
        });

    private static async Task<IResult> DisconnectAsync(
        Guid householdId, DisconnectCalendarRequest? request, HttpContext context,
        IAuthorizationService authorization, GoogleCalendarService service,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (userId.Result is not null) return userId.Result;
            if (request is null || request.ConnectionId == Guid.Empty)
                return HouseholdEndpoints.ValidationFailed(context,
                    new Dictionary<string, string[]> { ["connectionId"] = ["A calendar connection is required."] });
            await service.DisconnectAsync(userId.UserAccountId!.Value, request, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> ListEventsAsync(
        Guid householdId, DateTimeOffset? from, DateTimeOffset? to, string? cursor,
        HttpContext context, IAuthorizationService authorization, GoogleCalendarService service,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Member, cancellationToken);
            if (userId.Result is not null) return userId.Result;
            if (from is null || to is null)
                return Problem(context, 400, ApiProblemCodes.CalendarRangeInvalid,
                    "Both from and to are required.");
            return Results.Ok(await service.ListEventsAsync(
                householdId, from.Value, to.Value, cursor, cancellationToken));
        });

    private static async Task<IResult> GetDisplaySettingsAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        GoogleCalendarService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Member, cancellationToken);
            return userId.Result ?? Results.Ok(await service.GetDisplaySettingsAsync(
                householdId, cancellationToken));
        });

    private static async Task<IResult> GetEventCreationTargetAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        GoogleCalendarService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Member, cancellationToken);
            return userId.Result ?? Results.Ok(await service.GetEventCreationTargetAsync(
                householdId, cancellationToken));
        });

    private static async Task<IResult> UpdateEventCreationTargetAsync(
        Guid householdId, UpdateCalendarEventCreationTargetRequest? request,
        HttpContext context, IAuthorizationService authorization,
        GoogleCalendarService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (userId.Result is not null) return userId.Result;
            if (request is null)
                return HouseholdEndpoints.ValidationFailed(context,
                    new Dictionary<string, string[]> { ["sourceId"] = ["A target selection is required."] });
            return Results.Ok(await service.UpdateEventCreationTargetAsync(
                householdId, userId.UserAccountId!.Value, request, cancellationToken));
        });

    private static async Task<IResult> CreateEventAsync(
        Guid householdId, CreateCalendarEventRequest? request,
        HttpContext context, IAuthorizationService authorization,
        GoogleCalendarService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var userId = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Member, cancellationToken);
            if (userId.Result is not null) return userId.Result;
            if (request is null)
                return HouseholdEndpoints.ValidationFailed(context,
                    new Dictionary<string, string[]> { ["event"] = ["Event details are required."] });
            if (!context.User.TryGetUserSessionId(out var sessionId))
                return HouseholdEndpoints.AccountUnavailable(context);
            var created = await service.CreateEventAsync(
                householdId,
                userId.UserAccountId!.Value,
                sessionId,
                request,
                context.TraceIdentifier,
                cancellationToken);
            return Results.Created(
                $"/api/households/{householdId:D}/calendar/events/{Uri.EscapeDataString(created.Id)}",
                created);
        });

    private static async Task<IResult> GetManagedEventAsync(
        Guid householdId, Guid managementId, HttpContext context,
        IAuthorizationService authorization, CalendarEventManagementService service,
        CancellationToken cancellationToken) => await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            return access.Result ?? Results.Ok(await service.GetAsync(
                householdId, managementId, cancellationToken));
        });

    private static async Task<IResult> UpdateManagedEventAsync(
        Guid householdId, Guid managementId, UpdateCalendarEventRequest? request,
        HttpContext context, IAuthorizationService authorization,
        CalendarEventManagementService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (access.Result is not null) return access.Result;
            if (request is null)
                return HouseholdEndpoints.ValidationFailed(context,
                    new Dictionary<string, string[]> { ["event"] = ["Event details are required."] });
            if (!context.User.TryGetUserSessionId(out var sessionId))
                return HouseholdEndpoints.AccountUnavailable(context);
            return Results.Ok(await service.UpdateAsync(householdId, managementId,
                access.UserAccountId!.Value, sessionId, request, context.TraceIdentifier,
                cancellationToken));
        });

    private static async Task<IResult> DeleteManagedEventAsync(
        Guid householdId, Guid managementId, DeleteCalendarEventRequest? request,
        HttpContext context, IAuthorizationService authorization,
        CalendarEventManagementService service, CancellationToken cancellationToken) =>
        await RunAsync(context, async () =>
        {
            var access = await RequireAccessAsync(householdId, context, authorization,
                HouseholdAuthorizationPolicies.Administration, cancellationToken);
            if (access.Result is not null) return access.Result;
            if (request is null)
                return HouseholdEndpoints.ValidationFailed(context,
                    new Dictionary<string, string[]> { ["event"] = ["Delete confirmation is required."] });
            if (!context.User.TryGetUserSessionId(out var sessionId))
                return HouseholdEndpoints.AccountUnavailable(context);
            return Results.Ok(await service.DeleteAsync(householdId, managementId,
                access.UserAccountId!.Value, sessionId, request, context.TraceIdentifier,
                cancellationToken));
        });

    private static async Task<(Guid? UserAccountId, IResult? Result)> RequireAccessAsync(
        Guid householdId, HttpContext context, IAuthorizationService authorization,
        string policy, CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId))
            return (null, HouseholdEndpoints.AccountUnavailable(context));
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, householdId,
                HouseholdAuthorizationPolicies.Member, cancellationToken))
            return (null, HouseholdEndpoints.HouseholdNotFound(context));
        if (!await HouseholdEndpoints.HasAccessAsync(context, authorization, householdId,
                policy, cancellationToken))
            return (null, policy == HouseholdAuthorizationPolicies.Administration
                ? ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context)
                : HouseholdEndpoints.AdultAccessRequired(context));
        return (userAccountId, null);
    }

    private static async Task<IResult> RunAsync(HttpContext context, Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (CalendarOperationException exception)
        {
            return Problem(context, exception.Status, exception.Code, exception.Message);
        }
        catch (GoogleCalendarProviderException exception)
        {
            var (status, code) = exception.Failure == GoogleCalendarProviderFailure.RateLimited
                ? (429, ApiProblemCodes.CalendarProviderRateLimited)
                : (503, ApiProblemCodes.CalendarProviderUnavailable);
            return Problem(context, status, code, "Google Calendar is temporarily unavailable.");
        }
    }

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(ApiProblems.Create(context, status, code, title));
}
