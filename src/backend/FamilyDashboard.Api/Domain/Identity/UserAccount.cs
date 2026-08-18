using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Integrations;

namespace FamilyDashboard.Api.Domain.Identity;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DisplayName { get; set; }
    public required string PrimaryEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = [];
    public ICollection<UserSession> UserSessions { get; set; } = [];
    public ICollection<HouseholdMembership> HouseholdMemberships { get; set; } = [];
    public ICollection<HouseholdInvitation> CreatedHouseholdInvitations { get; set; } = [];
    public ICollection<HouseholdInvitation> AcceptedHouseholdInvitations { get; set; } = [];
    public ICollection<HouseholdInvitation> RevokedHouseholdInvitations { get; set; } = [];
    public ICollection<HouseholdAccessPin> ChangedHouseholdAccessPins { get; set; } = [];
    public ICollection<ParentAccessAuditEvent> ParentAccessAuditEvents { get; set; } = [];
    public GoogleCalendarConnection? GoogleCalendarConnection { get; set; }
    public ICollection<HouseholdCalendarSource> AddedHouseholdCalendarSources { get; set; } = [];
}
