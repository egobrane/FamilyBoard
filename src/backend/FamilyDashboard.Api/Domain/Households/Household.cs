using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Domain.Rewards;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class Household
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public HouseholdConfiguration? Configuration { get; set; }
    public HouseholdDashboardAppearance? DashboardAppearance { get; set; }
    public HouseholdWeatherConfiguration? WeatherConfiguration { get; set; }
    public ICollection<HouseholdPhotoAsset> PhotoAssets { get; set; } = [];
    public ICollection<HouseholdMemberPhotoAsset> MemberPhotoAssets { get; set; } = [];
    public ICollection<HouseholdMember> Members { get; set; } = [];
    public ICollection<HouseholdMembership> Memberships { get; set; } = [];
    public ICollection<HouseholdInvitation> Invitations { get; set; } = [];
    public ICollection<HouseholdCalendarSource> CalendarSources { get; set; } = [];
    public ICollection<HouseholdTaskListSource> TaskListSources { get; set; } = [];
    public ICollection<GoogleTaskMutationReceipt> GoogleTaskMutationReceipts { get; set; } = [];
    public ICollection<CalendarEventCreationReceipt> CalendarEventCreationReceipts { get; set; } = [];
    public ICollection<CalendarEventMutationReceipt> CalendarEventMutationReceipts { get; set; } = [];
    public HouseholdAccessPin? AccessPin { get; set; }
    public ICollection<ParentAccessAuditEvent> ParentAccessAuditEvents { get; set; } = [];
    public ICollection<ApplicationPreference> Preferences { get; set; } = [];
    public ICollection<ChoreDefinition> ChoreDefinitions { get; set; } = [];
    public ICollection<ChoreSchedule> ChoreSchedules { get; set; } = [];
    public ICollection<Reward> Rewards { get; set; } = [];
}
