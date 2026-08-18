using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Integrations;

public sealed class GoogleCalendarConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAccountId { get; set; }
    public required string ProviderSubject { get; set; }
    public required string ProviderEmailNormalized { get; set; }
    public string? ProtectedAccessToken { get; set; }
    public string? ProtectedRefreshToken { get; set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }
    public required string GrantedScopes { get; set; }
    public GoogleCalendarConnectionStatus Status { get; set; } = GoogleCalendarConnectionStatus.Active;
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSuccessfulRefreshAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public UserAccount UserAccount { get; set; } = null!;
    public ICollection<HouseholdCalendarSource> HouseholdSources { get; set; } = [];
}
