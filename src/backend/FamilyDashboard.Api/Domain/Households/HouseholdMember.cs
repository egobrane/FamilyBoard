using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Domain.Integrations;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public required string DisplayName { get; set; }
    public HouseholdMemberRole Role { get; set; }
    public string? AvatarColor { get; set; }
    public Guid? CurrentPhotoAssetId { get; set; }
    public decimal PhotoFocalX { get; set; } = 0.5m;
    public decimal PhotoFocalY { get; set; } = 0.5m;
    public long PhotoVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
    public HouseholdMembership? Membership { get; set; }
    public HouseholdMemberPhotoAsset? CurrentPhotoAsset { get; set; }
    public ICollection<HouseholdMemberPhotoAsset> PhotoAssets { get; set; } = [];
    public ICollection<HouseholdMemberPhotoAsset> CreatedPhotoAssets { get; set; } = [];
    public ICollection<ApplicationPreference> Preferences { get; set; } = [];
    public ICollection<ChoreAssignment> ChoreAssignments { get; set; } = [];
    public ICollection<ChoreSchedule> ChoreSchedules { get; set; } = [];
    public ICollection<PointTransaction> PointTransactions { get; set; } = [];
    public ICollection<PointTransaction> CreatedPointTransactions { get; set; } = [];
    public ICollection<RewardRedemption> RewardRedemptions { get; set; } = [];
    public ICollection<CalendarEventCreationReceipt> AttributedCalendarEventCreations { get; set; } = [];
    public ICollection<CalendarEventMutationReceipt> ActedCalendarEventMutations { get; set; } = [];
    public ICollection<GoogleTaskMutationReceipt> AttributedGoogleTaskMutations { get; set; } = [];
}
