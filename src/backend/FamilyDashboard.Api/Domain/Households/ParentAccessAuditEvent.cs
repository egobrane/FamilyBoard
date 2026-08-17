using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class ParentAccessAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid UserAccountId { get; set; }
    public Guid UserSessionId { get; set; }
    public ParentAccessAuditEventType EventType { get; set; }
    public ParentAccessAuditOutcome Outcome { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset? CooldownUntil { get; set; }

    public Household Household { get; set; } = null!;
    public UserAccount UserAccount { get; set; } = null!;
    public UserSession UserSession { get; set; } = null!;
}
