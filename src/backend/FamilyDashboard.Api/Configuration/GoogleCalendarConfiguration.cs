namespace FamilyDashboard.Api.Configuration;

public sealed class GoogleCalendarConfiguration
{
    public const string SectionName = "GoogleCalendar";

    public bool Enabled { get; init; }
    public bool EventCreationEnabled { get; init; }
    public bool EventManagementEnabled { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public TimeSpan AuthorizationLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan FreshCacheLifetime { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan StaleCacheLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public int MaximumCalendarsPerHousehold { get; init; } = 25;
    public int MaximumEventsPerRequest { get; init; } = 1000;
}
