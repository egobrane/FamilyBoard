using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Rewards;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public required string DisplayName { get; set; }
    public HouseholdMemberRole Role { get; set; }
    public string? AvatarColor { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
    public HouseholdMembership? Membership { get; set; }
    public ICollection<ApplicationPreference> Preferences { get; set; } = [];
    public ICollection<ChoreAssignment> ChoreAssignments { get; set; } = [];
    public ICollection<PointTransaction> PointTransactions { get; set; } = [];
    public ICollection<RewardRedemption> RewardRedemptions { get; set; } = [];
}
