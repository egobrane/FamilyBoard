using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Integrations;

public sealed class HouseholdTaskListSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid GoogleTasksConnectionId { get; set; }
    public Guid OwnerUserAccountId { get; set; }
    public required string ExternalTaskListId { get; set; }
    public required string DisplayNameSnapshot { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsWriteTarget { get; set; }
    public DateTimeOffset? WriteTargetConfiguredAt { get; set; }
    public Guid? WriteTargetConfiguredByUserAccountId { get; set; }
    public Guid AddedByUserAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Household Household { get; set; } = null!;
    public GoogleTasksConnection GoogleTasksConnection { get; set; } = null!;
    public UserAccount AddedByUserAccount { get; set; } = null!;
    public UserAccount? WriteTargetConfiguredByUserAccount { get; set; }
    public ICollection<GoogleTaskMutationReceipt> MutationReceipts { get; set; } = [];
}
