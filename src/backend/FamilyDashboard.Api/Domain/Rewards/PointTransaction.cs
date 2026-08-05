using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Rewards;

public sealed class PointTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public int Amount { get; set; }
    public PointTransactionType Type { get; set; }
    public required string Description { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? ChoreCompletionId { get; set; }
    public Guid? RewardRedemptionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
    public HouseholdMember HouseholdMember { get; set; } = null!;
    public ChoreCompletion? ChoreCompletion { get; set; }
    public RewardRedemption? RewardRedemption { get; set; }
}
