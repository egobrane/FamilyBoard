namespace FamilyDashboard.Api.Domain.Identity;

public sealed class ExternalIdentity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAccountId { get; set; }
    public required string Provider { get; set; }
    public required string ProviderSubject { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserAccount UserAccount { get; set; } = null!;
}
