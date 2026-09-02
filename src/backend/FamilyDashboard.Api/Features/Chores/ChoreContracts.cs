using FamilyDashboard.Api.Features.HouseholdMembers;

namespace FamilyDashboard.Api.Features.Chores;

public sealed record CreateChoreDefinitionRequest(Guid ClientRequestId, string Title, string? Description,
    int DefaultPointValue = 0);
public sealed record UpdateChoreDefinitionRequest(long ExpectedVersion, string Title, string? Description,
    int DefaultPointValue = 0);
public sealed record ChangeChoreDefinitionStateRequest(long ExpectedVersion);
public sealed record CreateChoreAssignmentRequest(
    Guid ClientRequestId,
    Guid ChoreDefinitionId,
    string AssignmentMode,
    Guid? AssignedMemberId,
    DateOnly DueLocalDate,
    TimeOnly? DueLocalTime);
public sealed record ClaimChoreAssignmentRequest(
    Guid ClientRequestId,
    long ExpectedAssignmentVersion,
    Guid HouseholdMemberId);
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
    string AssignmentMode,
    Guid? AssignedMemberId,
    ChoreRecurrenceRequest Recurrence,
    DateOnly StartLocalDate,
    DateOnly? EndLocalDate,
    TimeOnly? DueLocalTime);
public sealed record UpdateChoreScheduleRequest(
    long ExpectedVersion,
    Guid ChoreDefinitionId,
    string AssignmentMode,
    Guid? AssignedMemberId,
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
    int DefaultPointValue,
    bool IsActive,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ChoreParticipantResponse(
    Guid Id,
    string DisplayName,
    string Role,
    string? AvatarColor,
    HouseholdMemberPhotoResponse? Photo);

public sealed record ChoreCompletionResponse(
    Guid Id,
    Guid AssignmentId,
    ChoreParticipantResponse CompletedByMember,
    string Status,
    bool WasSharedDisplay,
    int PointValue,
    DateTimeOffset CompletedAt,
    ChoreParticipantResponse? ReviewedByMember,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    long Version,
    PointAwardResponse? Award);

public sealed record PointAwardResponse(Guid TransactionId, int Amount);

public sealed record ChoreAssignmentResponse(
    Guid Id,
    Guid ChoreDefinitionId,
    string Title,
    string? Description,
    int PointValue,
    string AssignmentMode,
    ChoreParticipantResponse? AssignedMember,
    DateTimeOffset? ClaimedAt,
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
    string AssignmentMode,
    ChoreParticipantResponse? AssignedMember,
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
    IReadOnlyList<ChoreAssignmentResponse> Open,
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
    AlreadyClaimed,
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
