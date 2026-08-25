using FamilyDashboard.Api.Features.Points;

namespace FamilyDashboard.Api.Features.Rewards;

public sealed record CreateRewardRequest(Guid ClientRequestId, string Title, string? Description, int PointCost);
public sealed record UpdateRewardRequest(long ExpectedVersion, string Title, string? Description, int PointCost);
public sealed record ChangeRewardStateRequest(long ExpectedVersion);
public sealed record CreateRewardRedemptionRequest(Guid ClientRequestId, Guid RewardId, Guid? HouseholdMemberId);
public sealed record ReviewRewardRedemptionRequest(long ExpectedVersion, string Decision, string? Note);
public sealed record FulfillRewardRedemptionRequest(long ExpectedVersion);
public sealed record CancelRewardRedemptionRequest(long ExpectedVersion, string Reason);

public sealed record RewardResponse(Guid Id, string Title, string? Description, int PointCost,
    bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record RewardRedemptionResponse(Guid Id, Guid RewardId, string RewardTitle,
    string? RewardDescription, int PointCost, PointMemberResponse HouseholdMember, string Status,
    PointMemberResponse? RequestedByMember, bool WasSharedDisplay, DateTimeOffset RequestedAt,
    PointMemberResponse? ReviewedByMember, DateTimeOffset? ReviewedAt, string? ReviewNote,
    PointMemberResponse? FulfilledByMember, DateTimeOffset? FulfilledAt,
    PointMemberResponse? CancelledByMember, DateTimeOffset? CancelledAt, string? CancellationReason,
    long Version);
public sealed record RewardCatalogResponse(IReadOnlyList<RewardResponse> Rewards,
    IReadOnlyList<PointMemberBalanceResponse> Members);
public sealed record RewardRedemptionListResponse(IReadOnlyList<RewardRedemptionResponse> Items,
    string? NextCursor);
public sealed record RewardOperationResult<T>(RewardOperationStatus Status, T? Value = default);

public enum RewardOperationStatus
{
    Success, NotFound, MemberNotFound, Inactive, MemberInactive, InsufficientPoints,
    IdempotencyConflict, RedemptionNotFound, RedemptionIdempotencyConflict,
    InvalidTransition, LegacyRequiresResolution, ConcurrencyConflict,
}
