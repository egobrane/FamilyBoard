namespace FamilyDashboard.Api.Configuration;

public sealed class DataProtectionConfiguration
{
    public const string SectionName = "DataProtection";

    public bool UseAzure { get; init; }
    public string ApplicationName { get; init; } = "FamilyDashboard";
    public string BlobUri { get; init; } = string.Empty;
    public string KeyIdentifier { get; init; } = string.Empty;
    public string ManagedIdentityClientId { get; init; } = string.Empty;
}
