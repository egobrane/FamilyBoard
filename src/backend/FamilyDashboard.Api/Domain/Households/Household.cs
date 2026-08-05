using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Rewards;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class Household
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public HouseholdConfiguration? Configuration { get; set; }
    public ICollection<HouseholdMember> Members { get; set; } = [];
    public ICollection<ApplicationPreference> Preferences { get; set; } = [];
    public ICollection<ChoreDefinition> ChoreDefinitions { get; set; } = [];
    public ICollection<Reward> Rewards { get; set; } = [];
}
