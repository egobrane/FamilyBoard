using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Chores;

public sealed class ChoreAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid ChoreDefinitionId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public Guid? CreatedByMemberId { get; set; }
    public Guid ClientRequestId { get; set; } = Guid.NewGuid();
    public required string TitleSnapshot { get; set; }
    public string? DescriptionSnapshot { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateOnly? DueLocalDate { get; set; }
    public TimeOnly? DueLocalTime { get; set; }
    public string? DueTimeZone { get; set; }
    public bool DueHasExplicitTime { get; set; }
    public ChoreAssignmentStatus Status { get; set; } = ChoreAssignmentStatus.Pending;
    public DateTimeOffset? SkippedAt { get; set; }
    public Guid? SkippedByMemberId { get; set; }
    public string? SkipReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; } = 1;

    public ChoreDefinition ChoreDefinition { get; set; } = null!;
    public HouseholdMember HouseholdMember { get; set; } = null!;
    public HouseholdMember? CreatedByMember { get; set; }
    public HouseholdMember? SkippedByMember { get; set; }
    public ICollection<ChoreCompletion> Completions { get; set; } = [];
}
