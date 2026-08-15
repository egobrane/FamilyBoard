using FamilyDashboard.Api.Domain.Households;

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
}
