namespace FamilyDashboard.Api.Configuration;

public sealed class HouseholdMediaConfiguration
{
    public const string SectionName = "HouseholdMedia";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "FileSystem";
    public string LocalPath { get; set; } = "data/household-photos";
    public string BlobContainerUri { get; set; } = string.Empty;
    public string ManagedIdentityClientId { get; set; } = string.Empty;
    public long MaximumUploadBytes { get; set; } = 10 * 1024 * 1024;
    public int MaximumPixelCount { get; set; } = 40_000_000;
    public int MaximumDimension { get; set; } = 12_000;
}
