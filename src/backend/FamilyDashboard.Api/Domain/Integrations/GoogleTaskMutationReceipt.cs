using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Integrations;

public sealed class GoogleTaskMutationReceipt
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid HouseholdTaskListSourceId { get; set; }
    public Guid GoogleTasksConnectionId { get; set; }
    public GoogleTaskMutationOperation Operation { get; set; }
    public GoogleTaskMutationReceiptStatus Status { get; set; } = GoogleTaskMutationReceiptStatus.Pending;
    public required byte[] RequestFingerprint { get; set; }
    public string? ProviderTaskId { get; set; }
    public string? ResultProviderETag { get; set; }
    public Guid RequestedByUserAccountId { get; set; }
    public Guid AttributedHouseholdMemberId { get; set; }
    public bool RequestedFromSharedDisplay { get; set; }
    public string? FailureCode { get; set; }
    public required string TraceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public Household Household { get; set; } = null!;
    public HouseholdTaskListSource HouseholdTaskListSource { get; set; } = null!;
    public GoogleTasksConnection GoogleTasksConnection { get; set; } = null!;
    public UserAccount RequestedByUserAccount { get; set; } = null!;
    public HouseholdMember AttributedHouseholdMember { get; set; } = null!;
}
