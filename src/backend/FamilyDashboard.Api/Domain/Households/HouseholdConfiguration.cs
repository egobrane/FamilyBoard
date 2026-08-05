namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdConfiguration
{
    public Guid HouseholdId { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string Locale { get; set; } = "en-US";
    public DayOfWeek WeekStartsOn { get; set; } = DayOfWeek.Sunday;
    public string Theme { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
}
