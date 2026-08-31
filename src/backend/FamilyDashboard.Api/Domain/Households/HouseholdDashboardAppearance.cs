namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdDashboardAppearance
{
    public Guid HouseholdId { get; set; }
    public string? GreetingTitle { get; set; }
    public string? GreetingMessage { get; set; }
    public Guid? CurrentPhotoAssetId { get; set; }
    public decimal PhotoFocalX { get; set; } = 0.5m;
    public decimal PhotoFocalY { get; set; } = 0.5m;
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
    public HouseholdPhotoAsset? CurrentPhotoAsset { get; set; }
}
