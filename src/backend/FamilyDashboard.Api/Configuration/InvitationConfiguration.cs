namespace FamilyDashboard.Api.Configuration;

public sealed class InvitationConfiguration
{
    public const string SectionName = "Invitations";

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan PendingCookieLifetime { get; init; } = TimeSpan.FromMinutes(30);
}
