namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdPhotoAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public required string StoragePrefix { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public long TotalByteLength { get; set; }
    public Guid CreatedByHouseholdMemberId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RetiredAt { get; set; }

    public Household Household { get; set; } = null!;
    public HouseholdMember CreatedByHouseholdMember { get; set; } = null!;
}
