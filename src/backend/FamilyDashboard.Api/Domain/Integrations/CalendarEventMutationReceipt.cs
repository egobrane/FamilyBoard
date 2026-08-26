using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Integrations;

public sealed class CalendarEventMutationReceipt
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid CalendarEventCreationReceiptId { get; set; }
    public Guid HouseholdCalendarSourceId { get; set; }
    public CalendarEventMutationOperation Operation { get; set; }
    public Guid RequestedByUserAccountId { get; set; }
    public Guid ActingHouseholdMemberId { get; set; }
    public bool RequestedFromSharedDisplay { get; set; }
    public required byte[] RequestFingerprint { get; set; }
    public required string ExpectedProviderVersion { get; set; }
    public string? ResultProviderVersion { get; set; }
    public CalendarEventMutationReceiptStatus Status { get; set; } = CalendarEventMutationReceiptStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public required string TraceId { get; set; }

    public Household Household { get; set; } = null!;
    public CalendarEventCreationReceipt CalendarEventCreationReceipt { get; set; } = null!;
    public HouseholdCalendarSource HouseholdCalendarSource { get; set; } = null!;
    public UserAccount RequestedByUserAccount { get; set; } = null!;
    public HouseholdMember ActingHouseholdMember { get; set; } = null!;
}
