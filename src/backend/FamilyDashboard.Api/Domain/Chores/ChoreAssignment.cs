using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Chores;

public sealed class ChoreAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChoreDefinitionId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public ChoreAssignmentStatus Status { get; set; } = ChoreAssignmentStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ChoreDefinition ChoreDefinition { get; set; } = null!;
    public HouseholdMember HouseholdMember { get; set; } = null!;
    public ChoreCompletion? Completion { get; set; }
}
