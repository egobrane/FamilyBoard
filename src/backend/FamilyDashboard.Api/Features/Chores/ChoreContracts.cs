namespace FamilyDashboard.Api.Features.Chores;

public sealed record CreateChoreDefinitionRequest(Guid ClientRequestId, string Title, string? Description);
public sealed record UpdateChoreDefinitionRequest(long ExpectedVersion, string Title, string? Description);
public sealed record ChangeChoreDefinitionStateRequest(long ExpectedVersion);
public sealed record CreateChoreAssignmentRequest(
    Guid ClientRequestId,
    Guid ChoreDefinitionId,
    Guid AssignedMemberId,
    DateOnly DueLocalDate,
    TimeOnly? DueLocalTime);
public sealed record CompleteChoreRequest(
    Guid ClientRequestId,
    long ExpectedAssignmentVersion,
    Guid? CompletedByMemberId);
public sealed record SkipChoreAssignmentRequest(long ExpectedVersion, string? Reason);
public sealed record ReviewChoreCompletionRequest(long ExpectedVersion, string Decision, string? Note);
public sealed record ChoreRecurrenceRequest(string Kind, int Interval, IReadOnlyList<string>? DaysOfWeek);
public sealed record CreateChoreScheduleRequest(
    Guid ClientRequestId,
    Guid ChoreDefinitionId,
    Guid AssignedMemberId,
    ChoreRecurrenceRequest Recurrence,
    DateOnly StartLocalDate,
    DateOnly? EndLocalDate,
    TimeOnly? DueLocalTime);
public sealed record UpdateChoreScheduleRequest(
    long ExpectedVersion,
    Guid ChoreDefinitionId,
    Guid AssignedMemberId,
    ChoreRecurrenceRequest Recurrence,
    DateOnly StartLocalDate,
    DateOnly? EndLocalDate,
    TimeOnly? DueLocalTime);
public sealed record ChangeChoreScheduleStateRequest(long ExpectedVersion);
public sealed record PreviewChoreScheduleRequest(
    ChoreRecurrenceRequest Recurrence,
    DateOnly StartLocalDate,
    DateOnly? EndLocalDate);

public sealed record ChoreDefinitionResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsActive,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ChoreParticipantResponse(
    Guid Id,
    string DisplayName,
    string Role,
    string? AvatarColor);

public sealed record ChoreCompletionResponse(
    Guid Id,
    Guid AssignmentId,
    ChoreParticipantResponse CompletedByMember,
    string Status,
    bool WasSharedDisplay,
    DateTimeOffset CompletedAt,
    ChoreParticipantResponse? ReviewedByMember,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    long Version);

public sealed record ChoreAssignmentResponse(
    Guid Id,
    Guid ChoreDefinitionId,
    string Title,
    string? Description,
    ChoreParticipantResponse AssignedMember,
    DateOnly? DueLocalDate,
    TimeOnly? DueLocalTime,
    DateTimeOffset? DueAt,
    string? DueTimeZone,
    bool DueHasExplicitTime,
    string Status,
    bool IsOverdue,
    long Version,
    ChoreCompletionResponse? PendingCompletion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ChoreScheduleResponse(
    Guid Id,
    ChoreDefinitionResponse Definition,
    ChoreParticipantResponse AssignedMember,
    ChoreRecurrenceRequest Recurrence,
    DateOnly StartLocalDate,
    DateOnly? EndLocalDate,
    TimeOnly? DueLocalTime,
    string TimeZone,
    string Status,
    string? BlockedReason,
    DateOnly? NextOccurrenceLocalDate,
    DateOnly? LastGeneratedOccurrenceLocalDate,
    DateTimeOffset? LastEvaluatedAt,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ChoreSchedulePreviewResponse(IReadOnlyList<DateOnly> Occurrences);

public sealed record ChoreDashboardResponse(
    IReadOnlyList<ChoreAssignmentResponse> Overdue,
    IReadOnlyList<ChoreAssignmentResponse> DueToday,
    IReadOnlyList<ChoreAssignmentResponse> Upcoming,
    int AwaitingReviewCount);

public sealed record ChoreListResponse(
    IReadOnlyList<ChoreAssignmentResponse> Items,
    string? NextCursor);

public sealed record ChoreOperationResult<T>(
    ChoreOperationStatus Status,
    T? Value = default);

public enum ChoreOperationStatus
{
    Success,
    NotFound,
    DefinitionInactive,
    MemberInactive,
    NotActionable,
    PendingReview,
    AlreadyReviewed,
    IdempotencyConflict,
    ConcurrencyConflict,
    InvalidDueDate,
}

public enum ChoreScheduleOperationStatus
{
    Success,
    NotFound,
    DefinitionInactive,
    MemberInactive,
    IdempotencyConflict,
    ConcurrencyConflict,
    InvalidSchedule,
    DependencyInactive,
}

public sealed record ChoreScheduleOperationResult<T>(ChoreScheduleOperationStatus Status, T? Value = default);
