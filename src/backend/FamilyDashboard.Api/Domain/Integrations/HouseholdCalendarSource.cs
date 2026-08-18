using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Integrations;

public sealed class HouseholdCalendarSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid GoogleCalendarConnectionId { get; set; }
    public Guid OwnerUserAccountId { get; set; }
    public required string ExternalCalendarId { get; set; }
    public required string DisplayNameSnapshot { get; set; }
    public string? TimeZone { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid AddedByUserAccountId { get; set; }

    public Household Household { get; set; } = null!;
    public GoogleCalendarConnection GoogleCalendarConnection { get; set; } = null!;
    public UserAccount AddedByUserAccount { get; set; } = null!;
}
