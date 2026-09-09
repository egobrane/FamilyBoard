using FamilyDashboard.Api.Features.HouseholdMembers;

namespace FamilyDashboard.Api.Features.Points;

public sealed record PointMemberResponse(Guid Id, string DisplayName, string Role,
    string? AvatarColor, bool IsActive, HouseholdMemberPhotoResponse? Photo);

public sealed record PointMemberBalanceResponse(Guid MemberId, string DisplayName, string Role,
    string? AvatarColor, bool IsActive, long Balance, HouseholdMemberPhotoResponse? Photo);

public sealed record PointTransactionResponse(
    Guid Id,
    PointMemberResponse HouseholdMember,
    int Amount,
    string Type,
    string Description,
    Guid? ChoreCompletionId,
    Guid? RewardRedemptionId,
    Guid? PointDeductionDefinitionId,
    Guid? ReversesPointTransactionId,
    PointMemberResponse? CreatedByMember,
    DateTimeOffset CreatedAt,
    bool IsReversed);

public sealed record HouseholdPointSummaryResponse(
    long HouseholdBalance,
    IReadOnlyList<PointMemberBalanceResponse> Members,
    IReadOnlyList<PointTransactionResponse> RecentTransactions);

public sealed record PointTransactionListResponse(
    IReadOnlyList<PointTransactionResponse> Items,
    string? NextCursor);

public sealed record CreatePointAdjustmentRequest(
    Guid ClientRequestId,
    Guid HouseholdMemberId,
    int Amount,
    string Reason);

public sealed record ReversePointTransactionRequest(Guid ClientRequestId, string Reason);

public sealed record CreatePointDeductionDefinitionRequest(
    Guid ClientRequestId,
    string Title,
    int DeductionAmount);

public sealed record UpdatePointDeductionDefinitionRequest(
    long ExpectedVersion,
    string Title,
    int DeductionAmount);

public sealed record ChangePointDeductionDefinitionStateRequest(long ExpectedVersion);

public sealed record ApplyPointDeductionRequest(
    Guid ClientRequestId,
    Guid HouseholdMemberId,
    long ExpectedDefinitionVersion);

public sealed record PointDeductionDefinitionResponse(
    Guid Id,
    string Title,
    int DeductionAmount,
    bool IsActive,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PointOperationResult<T>(PointOperationStatus Status, T? Value = default);

public enum PointOperationStatus
{
    Success,
    MemberNotFound,
    MemberInactive,
    DeductionDefinitionNotFound,
    DeductionDefinitionInactive,
    TransactionNotFound,
    IdempotencyConflict,
    AlreadyReversed,
    NotReversible,
    ConcurrencyConflict,
}
