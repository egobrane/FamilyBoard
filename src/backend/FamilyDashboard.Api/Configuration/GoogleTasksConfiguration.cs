namespace FamilyDashboard.Api.Configuration;

public sealed class GoogleTasksConfiguration
{
    public const string SectionName = "GoogleTasks";

    public bool Enabled { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public TimeSpan AuthorizationLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan FreshCacheLifetime { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan StaleCacheLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public int MaximumTaskListsPerHousehold { get; init; } = 25;
    public int MaximumTasksPerRequest { get; init; } = 200;
}
