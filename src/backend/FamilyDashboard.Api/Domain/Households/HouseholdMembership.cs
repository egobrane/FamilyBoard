using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdMembership
{
    public Guid UserAccountId { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserAccount UserAccount { get; set; } = null!;
    public Household Household { get; set; } = null!;
    public HouseholdMember HouseholdMember { get; set; } = null!;
}
