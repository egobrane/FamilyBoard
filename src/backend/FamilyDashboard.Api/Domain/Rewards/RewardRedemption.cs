using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Rewards;

public sealed class RewardRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RewardId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public int PointCostSnapshot { get; set; }
    public RedemptionStatus Status { get; set; } = RedemptionStatus.Requested;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByMemberId { get; set; }

    public Reward Reward { get; set; } = null!;
    public HouseholdMember HouseholdMember { get; set; } = null!;
    public HouseholdMember? ReviewedByMember { get; set; }
    public PointTransaction? PointTransaction { get; set; }
}
