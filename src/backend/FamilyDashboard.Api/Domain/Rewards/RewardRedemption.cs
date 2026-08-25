using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Rewards;

public sealed class RewardRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid ClientRequestId { get; set; }
    public Guid RewardId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public required string RewardTitleSnapshot { get; set; }
    public string? RewardDescriptionSnapshot { get; set; }
    public int PointCostSnapshot { get; set; }
    public RedemptionStatus Status { get; set; } = RedemptionStatus.Requested;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RequestedByUserAccountId { get; set; }
    public Guid? RequestedByMemberId { get; set; }
    public bool WasSharedDisplay { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByMemberId { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset? FulfilledAt { get; set; }
    public Guid? FulfilledByMemberId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledByMemberId { get; set; }
    public string? CancellationReason { get; set; }
    public long Version { get; set; } = 1;

    public Reward Reward { get; set; } = null!;
    public HouseholdMember HouseholdMember { get; set; } = null!;
    public HouseholdMember? RequestedByMember { get; set; }
    public HouseholdMember? ReviewedByMember { get; set; }
    public HouseholdMember? FulfilledByMember { get; set; }
    public HouseholdMember? CancelledByMember { get; set; }
    public PointTransaction? PointTransaction { get; set; }
}
