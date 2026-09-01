namespace FamilyDashboard.Api.Domain.Households;

public enum HouseholdMemberPhotoAssetState
{
    Pending,
    Active,
    Retired,
}

public sealed class HouseholdMemberPhotoAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public required string StoragePrefix { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public long TotalByteLength { get; set; }
    public HouseholdMemberPhotoAssetState State { get; set; } = HouseholdMemberPhotoAssetState.Pending;
    public Guid CreatedByHouseholdMemberId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }

    public Household Household { get; set; } = null!;
    public HouseholdMember HouseholdMember { get; set; } = null!;
    public HouseholdMember CreatedByHouseholdMember { get; set; } = null!;
}
