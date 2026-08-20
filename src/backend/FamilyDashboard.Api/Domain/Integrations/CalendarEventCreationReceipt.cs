using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Integrations;

public sealed class CalendarEventCreationReceipt
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid HouseholdCalendarSourceId { get; set; }
    public Guid RequestedByUserAccountId { get; set; }
    public Guid AttributedHouseholdMemberId { get; set; }
    public bool RequestedFromSharedDisplay { get; set; }
    public required byte[] RequestFingerprint { get; set; }
    public string? ProviderEventId { get; set; }
    public CalendarEventCreationReceiptStatus Status { get; set; } = CalendarEventCreationReceiptStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public required string TraceId { get; set; }

    public Household Household { get; set; } = null!;
    public HouseholdCalendarSource HouseholdCalendarSource { get; set; } = null!;
    public UserAccount RequestedByUserAccount { get; set; } = null!;
    public HouseholdMember AttributedHouseholdMember { get; set; } = null!;
}
