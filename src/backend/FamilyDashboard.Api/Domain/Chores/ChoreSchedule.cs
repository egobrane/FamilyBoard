using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Chores;

public sealed class ChoreSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid ChoreDefinitionId { get; set; }
    public Guid? HouseholdMemberId { get; set; }
    public ChoreAssignmentMode AssignmentMode { get; set; } = ChoreAssignmentMode.Assigned;
    public Guid CreatedByMemberId { get; set; }
    public Guid ClientRequestId { get; set; } = Guid.NewGuid();
    public ChoreRecurrenceKind RecurrenceKind { get; set; }
    public int Interval { get; set; } = 1;
    public int? DaysOfWeekMask { get; set; }
    public DateOnly StartLocalDate { get; set; }
    public DateOnly? EndLocalDate { get; set; }
    public TimeOnly? DueLocalTime { get; set; }
    public ChoreScheduleStatus Status { get; set; } = ChoreScheduleStatus.Active;
    public string? BlockedReason { get; set; }
    public DateOnly? NextOccurrenceLocalDate { get; set; }
    public DateOnly? LastGeneratedOccurrenceLocalDate { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;

    public Household Household { get; set; } = null!;
    public ChoreDefinition ChoreDefinition { get; set; } = null!;
    public HouseholdMember? HouseholdMember { get; set; }
    public HouseholdMember CreatedByMember { get; set; } = null!;
    public ICollection<ChoreAssignment> Assignments { get; set; } = [];
}
