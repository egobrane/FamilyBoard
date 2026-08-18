namespace FamilyDashboard.Api.Features.Calendar;

public sealed record CalendarConnectionResponse(
    bool IsAvailable,
    Guid? ConnectionId,
    string Status,
    string? ProviderEmail,
    DateTimeOffset? ConnectedAt,
    bool CanManageConnection,
    int ActiveSourceCount);

public sealed record BeginCalendarAuthorizationRequest(string? ReturnPath);
public sealed record BeginCalendarAuthorizationResponse(string AuthorizationUrl, DateTimeOffset ExpiresAt);

public sealed record ProviderCalendarResponse(
    string Id,
    string Name,
    string? TimeZone,
    string? Color,
    bool IsPrimary,
    bool IsSelected);

public sealed record CalendarSourceResponse(
    Guid Id,
    Guid ConnectionId,
    string ExternalCalendarId,
    string Name,
    string? TimeZone,
    string? Color,
    bool IsActive,
    bool IsOwnedByCurrentAdult);

public sealed record UpdateCalendarSourcesRequest(Guid ConnectionId, string[]? ExternalCalendarIds);

public sealed record DisconnectCalendarRequest(Guid ConnectionId, bool ConfirmGlobalDisconnect);

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
    string? Color);

public sealed record CalendarWarningResponse(Guid SourceId, string Code, string Message);

public sealed record CalendarEventsResponse(
    IReadOnlyList<CalendarEventResponse> Events,
    string? NextCursor,
    bool IsStale,
    IReadOnlyList<CalendarWarningResponse> Warnings);
