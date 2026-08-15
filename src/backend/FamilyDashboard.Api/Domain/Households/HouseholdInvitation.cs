using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid CreatedByUserAccountId { get; set; }
    public required string IntendedEmailNormalized { get; set; }
    public required byte[] TokenHash { get; set; }
    public HouseholdInvitationStatus Status { get; set; } = HouseholdInvitationStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedByUserAccountId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedByUserAccountId { get; set; }

    public Household Household { get; set; } = null!;
    public UserAccount CreatedByUserAccount { get; set; } = null!;
    public UserAccount? AcceptedByUserAccount { get; set; }
    public UserAccount? RevokedByUserAccount { get; set; }
}
