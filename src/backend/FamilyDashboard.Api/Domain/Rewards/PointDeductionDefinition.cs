using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Rewards;

public sealed class PointDeductionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid ClientRequestId { get; set; }
    public required string Title { get; set; }
    public int DeductionAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByMemberId { get; set; }
    public Guid? UpdatedByMemberId { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
    public HouseholdMember? CreatedByMember { get; set; }
    public HouseholdMember? UpdatedByMember { get; set; }
    public ICollection<PointTransaction> Applications { get; set; } = [];
}
