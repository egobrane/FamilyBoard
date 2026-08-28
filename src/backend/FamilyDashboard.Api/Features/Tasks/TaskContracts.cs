namespace FamilyDashboard.Api.Features.Tasks;

public sealed record TasksConnectionResponse(
    bool IsAvailable,
    Guid? ConnectionId,
    string Status,
    string? ProviderEmail,
    DateTimeOffset? ConnectedAt,
    int ActiveSourceCount,
    int ActiveHouseholdCount,
    bool CanRead,
    bool CanWrite,
    bool WriteAuthorizationRequired,
    bool MutationsAvailable);

public sealed record BeginTasksAuthorizationRequest(string? ReturnPath, string? Capability = null);
public sealed record BeginTasksAuthorizationResponse(string AuthorizationUrl, DateTimeOffset ExpiresAt);

public sealed record ProviderTaskListResponse(
    string Id, string Name, bool IsSelected, bool CanWrite, bool IsWriteTarget);

public sealed record TaskListSourceResponse(
    Guid Id,
    Guid ConnectionId,
    string ExternalTaskListId,
    string Name,
    bool IsActive,
    bool IsOwnedByCurrentAdult,
    bool CanWrite,
    bool IsWriteTarget);

public sealed record UpdateTaskListSourcesRequest(Guid ConnectionId, string[]? ExternalTaskListIds);
public sealed record DisconnectTasksRequest(Guid ConnectionId, bool ConfirmGlobalDisconnect);
public sealed record UpdateTaskWriteTargetRequest(Guid? SourceId);
public sealed record TaskWriteTargetResponse(
    bool IsAvailable, bool IsAuthorized, Guid? SourceId, string? Name);

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
    bool IsAssigned,
    bool CanChangeStatus,
    string? MutationVersion);

public sealed record CreateGoogleTaskRequest(
    Guid IdempotencyKey,
    Guid? AttributedMemberId,
    string? Title,
    string? Notes,
    string? DueDate);

public sealed record UpdateGoogleTaskStatusRequest(
    Guid SourceId,
    string? TaskId,
    Guid IdempotencyKey,
    Guid? AttributedMemberId,
    string? TargetStatus,
    string? MutationVersion);

public sealed record GoogleTaskMutationResponse(
    string Operation,
    string TaskId,
    Guid SourceId,
    string Status,
    string? DueDate,
    string MutationVersion,
    Guid AttributedMemberId,
    bool RecoveredExistingMutation);

public sealed record GoogleTasksWarningResponse(Guid SourceId, string Code, string Message);

public sealed record GoogleTasksResponse(
    IReadOnlyList<GoogleTaskResponse> Tasks,
    string? NextCursor,
    bool IsStale,
    IReadOnlyList<GoogleTasksWarningResponse> Warnings,
    bool CanCreateTasks);
