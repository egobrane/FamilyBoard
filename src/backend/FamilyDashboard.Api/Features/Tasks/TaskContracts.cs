namespace FamilyDashboard.Api.Features.Tasks;

public sealed record TasksConnectionResponse(
    bool IsAvailable,
    Guid? ConnectionId,
    string Status,
    string? ProviderEmail,
    DateTimeOffset? ConnectedAt,
    int ActiveSourceCount,
    int ActiveHouseholdCount);

public sealed record BeginTasksAuthorizationRequest(string? ReturnPath);
public sealed record BeginTasksAuthorizationResponse(string AuthorizationUrl, DateTimeOffset ExpiresAt);

public sealed record ProviderTaskListResponse(string Id, string Name, bool IsSelected);

public sealed record TaskListSourceResponse(
    Guid Id,
    Guid ConnectionId,
    string ExternalTaskListId,
    string Name,
    bool IsActive,
    bool IsOwnedByCurrentAdult);

public sealed record UpdateTaskListSourcesRequest(Guid ConnectionId, string[]? ExternalTaskListIds);
public sealed record DisconnectTasksRequest(Guid ConnectionId, bool ConfirmGlobalDisconnect);

public sealed record GoogleTaskResponse(
    string Id,
    Guid SourceId,
    string TaskListName,
    string Title,
    string? Notes,
    string Status,
    string? DueDate,
    DateTimeOffset? CompletedAt,
    string? ParentTaskId,
    string Position,
    bool IsSubtask,
    bool IsAssigned);

public sealed record GoogleTasksWarningResponse(Guid SourceId, string Code, string Message);

public sealed record GoogleTasksResponse(
    IReadOnlyList<GoogleTaskResponse> Tasks,
    string? NextCursor,
    bool IsStale,
    IReadOnlyList<GoogleTasksWarningResponse> Warnings);
