using FamilyDashboard.Api.Domain.Identity;

namespace FamilyDashboard.Api.Domain.Households;

public sealed class HouseholdAccessPin
{
    public Guid HouseholdId { get; set; }
    public required byte[] PinHash { get; set; }
    public required byte[] Salt { get; set; }
    public short HashVersion { get; set; }
    public int WorkFactor { get; set; }
    public short PepperVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public Guid ChangedByUserAccountId { get; set; }

    public Household Household { get; set; } = null!;
    public UserAccount ChangedByUserAccount { get; set; } = null!;
}
