namespace FamilyDashboard.Api.Configuration;

public sealed class ParentAccessConfiguration
{
    public const string SectionName = "ParentAccess";

    public bool Enabled { get; init; }
    public string Pepper { get; init; } = string.Empty;
    public short PepperVersion { get; init; } = 1;
    public int PinLength { get; init; } = 6;
    public int WorkFactor { get; init; } = 600_000;
    public TimeSpan ElevationLifetime { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan RecentAuthenticationLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public int MaximumFailures { get; init; } = 5;
    public TimeSpan FailureWindow { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan LockoutLifetime { get; init; } = TimeSpan.FromMinutes(15);
}
