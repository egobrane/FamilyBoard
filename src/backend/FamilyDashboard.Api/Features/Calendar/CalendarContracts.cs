namespace FamilyDashboard.Api.Features.Calendar;

public sealed record CalendarConnectionResponse(
    bool IsAvailable,
    Guid? ConnectionId,
    string Status,
    string? ProviderEmail,
    DateTimeOffset? ConnectedAt,
    bool CanManageConnection,
    int ActiveSourceCount,
    bool EventCreationAvailable,
    bool EventCreationAuthorized);

public sealed record BeginCalendarAuthorizationRequest(string? ReturnPath, string? Capability = null);
public sealed record BeginCalendarAuthorizationResponse(string AuthorizationUrl, DateTimeOffset ExpiresAt);

public sealed record ProviderCalendarResponse(
    string Id,
    string Name,
    string? TimeZone,
    string? Color,
    bool IsPrimary,
    bool IsSelected,
    string AccessRole,
    bool CanCreateEvents,
    bool IsEventCreationTarget);

public sealed record CalendarSourceResponse(
    Guid Id,
    Guid ConnectionId,
    string ExternalCalendarId,
    string Name,
    string? TimeZone,
    string? Color,
    bool IsActive,
    bool IsOwnedByCurrentAdult,
    bool IsEventCreationTarget);

public sealed record UpdateCalendarSourcesRequest(Guid ConnectionId, string[]? ExternalCalendarIds);

public sealed record DisconnectCalendarRequest(Guid ConnectionId, bool ConfirmGlobalDisconnect);

public sealed record UpdateCalendarEventCreationTargetRequest(Guid? SourceId);

public sealed record CalendarEventCreationTargetResponse(
    bool IsAvailable,
    bool IsAuthorized,
    Guid? SourceId,
    string? Name,
    string? TimeZone,
    string? Color);

public sealed record CreateCalendarEventRequest(
    Guid SourceId,
    Guid IdempotencyKey,
    Guid? AttributedMemberId,
    string? Title,
    string? Location,
    string? Notes,
    bool IsAllDay,
    string? Start,
    string? End,
    string? TimeZone);

public sealed record CreatedCalendarEventResponse(
    string Id,
    Guid SourceId,
    string CalendarName,
    string Title,
    bool IsAllDay,
    string Start,
    string End,
    string? TimeZone,
    string? Location,
    string? Color,
    Guid AttributedMemberId,
    bool RecoveredExistingEvent);

public sealed record CalendarEventResponse(
    string Id,
    Guid SourceId,
    string CalendarName,
    string Title,
    bool IsAllDay,
    string Start,
    string End,
    string? TimeZone,
    string? Location,
    string? Color,
    bool CanEdit = false,
    bool CanDelete = false,
    Guid? ManagementId = null,
    string? ProviderVersion = null,
    string? ManagementUnavailableReason = null);

public sealed record ManagedCalendarEventResponse(
    Guid ManagementId,
    Guid SourceId,
    string CalendarName,
    string Title,
    string? Location,
    string? Notes,
    bool IsAllDay,
    string Start,
    string End,
    string? TimeZone,
    string ProviderVersion,
    bool CanEdit,
    bool CanDelete,
    string? ManagementUnavailableReason);

public sealed record UpdateCalendarEventRequest(
    Guid IdempotencyKey,
    string? ExpectedProviderVersion,
    string? Title,
    string? Location,
    string? Notes,
    bool IsAllDay,
    string? Start,
    string? End,
    string? TimeZone);

public sealed record DeleteCalendarEventRequest(
    Guid IdempotencyKey,
    string? ExpectedProviderVersion,
    bool ConfirmDelete);

public sealed record CalendarEventMutationResponse(
    string Operation,
    DateTimeOffset CompletedAt,
    bool RecoveredExistingMutation,
    ManagedCalendarEventResponse? Event);

public sealed record CalendarWarningResponse(Guid SourceId, string Code, string Message);

public sealed record CalendarDisplaySettingsResponse(
    Guid HouseholdId,
    string TimeZone,
    string Locale,
    string WeekStartsOn);

public sealed record CalendarEventsResponse(
    IReadOnlyList<CalendarEventResponse> Events,
    string? NextCursor,
    bool IsStale,
    IReadOnlyList<CalendarWarningResponse> Warnings);
