namespace FamilyDashboard.Api.Configuration;

public sealed class AuthenticationConfiguration
{
    public const string SectionName = "Authentication";

    public string FrontendOrigin { get; init; } = "http://localhost:5173";
    public TimeSpan SessionIdleLifetime { get; init; } = TimeSpan.FromDays(14);
    public TimeSpan SessionAbsoluteLifetime { get; init; } = TimeSpan.FromDays(30);
    public TimeSpan SharedDisplayIdleLifetime { get; init; } = TimeSpan.FromDays(30);
    public TimeSpan SharedDisplayAbsoluteLifetime { get; init; } = TimeSpan.FromDays(90);
    public TimeSpan LastSeenWriteInterval { get; init; } = TimeSpan.FromMinutes(15);
    public GoogleAuthenticationConfiguration Google { get; init; } = new();
}

public sealed class GoogleAuthenticationConfiguration
{
    public bool Enabled { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}
