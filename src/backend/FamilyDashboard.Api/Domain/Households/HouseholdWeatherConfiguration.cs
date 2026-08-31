namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdWeatherConfiguration
{
    public Guid HouseholdId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public required string LocationLabel { get; set; }
    public string TemperatureUnit { get; set; } = "auto";
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
}
