using FamilyDashboard.Api.Domain.Households;

namespace FamilyDashboard.Api.Domain.Identity;

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? DeviceLabel { get; set; }
    public bool IsSharedDisplay { get; set; }
    public DateTimeOffset? AdministrativeElevationExpiresAt { get; set; }
    public Guid? AdministrativeElevationHouseholdId { get; set; }
    public int ParentAccessFailedAttemptCount { get; set; }
    public DateTimeOffset? ParentAccessFailureWindowStartedAt { get; set; }
    public DateTimeOffset? ParentAccessLockedUntil { get; set; }
    public Guid? SelectedHouseholdId { get; set; }

    public UserAccount UserAccount { get; set; } = null!;
    public HouseholdMembership? SelectedHouseholdMembership { get; set; }
    public HouseholdMembership? AdministrativeElevationHouseholdMembership { get; set; }
    public ICollection<ParentAccessAuditEvent> ParentAccessAuditEvents { get; set; } = [];
}
