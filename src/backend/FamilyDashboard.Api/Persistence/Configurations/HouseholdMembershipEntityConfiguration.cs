using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdMembershipEntityConfiguration : IEntityTypeConfiguration<HouseholdMembership>
{
    public void Configure(EntityTypeBuilder<HouseholdMembership> builder)
    {
        builder.ToTable("HouseholdMemberships");
        builder.HasKey(membership => new { membership.UserAccountId, membership.HouseholdId });
        builder.Property(membership => membership.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne(membership => membership.UserAccount)
            .WithMany(account => account.HouseholdMemberships)
            .HasForeignKey(membership => membership.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(membership => membership.Household)
            .WithMany(household => household.Memberships)
            .HasForeignKey(membership => membership.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(membership => membership.HouseholdMember)
            .WithOne(member => member.Membership)
            .HasForeignKey<HouseholdMembership>(membership => new
            {
                membership.HouseholdId,
                membership.HouseholdMemberId,
            })
            .HasPrincipalKey<HouseholdMember>(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
