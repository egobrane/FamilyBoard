namespace FamilyDashboard.Api.Features.ParentAccess;

public sealed record SetParentPinRequest(string Pin);
public sealed record VerifyParentPinRequest(string Pin);
public sealed record UpdateSharedDisplayRequest(
    Guid HouseholdId,
    bool IsSharedDisplay,
    string? DeviceLabel = null);

public sealed record ParentAccessStateResponse(
    Guid HouseholdId,
    bool IsPinConfigured,
    int PinLength,
    bool IsSharedDisplay,
    bool IsElevated,
    DateTimeOffset? ElevationExpiresAt,
    DateTimeOffset? LockedUntil);

public enum ParentAccessOperationStatus
{
    Success,
    Unavailable,
    SessionUnavailable,
    HouseholdNotFound,
    PinNotConfigured,
    PinAlreadyConfigured,
    InvalidPin,
    Locked,
    ElevationRequired,
    RecentAuthenticationRequired,
    PrivateSessionRequired,
    SharedDisplayRequiresPin,
    Conflict,
}

public sealed record ParentAccessOperationResult(
    ParentAccessOperationStatus Status,
    ParentAccessStateResponse? State = null,
    DateTimeOffset? RetryAt = null);
