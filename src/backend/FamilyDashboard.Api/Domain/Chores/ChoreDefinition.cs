using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Chores;

public sealed class ChoreDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid ClientRequestId { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int DefaultPointValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;

    public Household Household { get; set; } = null!;
    public ICollection<ChoreAssignment> Assignments { get; set; } = [];
    public ICollection<ChoreSchedule> Schedules { get; set; } = [];
}
