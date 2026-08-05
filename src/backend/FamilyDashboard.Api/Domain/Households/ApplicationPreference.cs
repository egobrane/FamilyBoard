namespace FamilyDashboard.Api.Domain.Households;

public sealed class ApplicationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid? HouseholdMemberId { get; set; }
    public required string Key { get; set; }
    public string ValueJson { get; set; } = "null";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
    public HouseholdMember? HouseholdMember { get; set; }
}
