using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Rewards;

namespace FamilyDashboard.Api.Domain.Chores;

public sealed class ChoreCompletion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChoreAssignmentId { get; set; }
    public Guid CompletedByMemberId { get; set; }
    public Guid? ReviewedByMemberId { get; set; }
    public ChoreCompletionStatus Status { get; set; } = ChoreCompletionStatus.PendingReview;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }

    public ChoreAssignment ChoreAssignment { get; set; } = null!;
    public HouseholdMember CompletedByMember { get; set; } = null!;
    public HouseholdMember? ReviewedByMember { get; set; }
    public PointTransaction? PointTransaction { get; set; }
}
