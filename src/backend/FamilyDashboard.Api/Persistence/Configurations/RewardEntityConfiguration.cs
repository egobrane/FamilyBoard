using FamilyDashboard.Api.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class RewardEntityConfiguration : IEntityTypeConfiguration<Reward>
{
    public void Configure(EntityTypeBuilder<Reward> builder)
    {
        builder.ToTable("Rewards", table =>
        {
            table.HasCheckConstraint("CK_Rewards_PointCost", "\"PointCost\" BETWEEN 1 AND 10000");
            table.HasCheckConstraint("CK_Rewards_Version", "\"Version\" > 0");
            table.HasCheckConstraint("CK_Rewards_ClientRequestId", "\"ClientRequestId\" <> '00000000-0000-0000-0000-000000000000'");
        });
        builder.HasKey(reward => reward.Id);
        builder.HasAlternateKey(reward => new { reward.HouseholdId, reward.Id });
        builder.Property(reward => reward.Title).HasMaxLength(120).IsRequired();
        builder.Property(reward => reward.Description).HasMaxLength(500);
        builder.Property(reward => reward.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.Property(reward => reward.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(reward => reward.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(reward => new { reward.HouseholdId, reward.IsActive });
        builder.HasIndex(reward => new { reward.HouseholdId, reward.ClientRequestId }).IsUnique();
        builder.HasOne(reward => reward.Household)
            .WithMany(household => household.Rewards)
            .HasForeignKey(reward => reward.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reward => reward.CreatedByMember).WithMany()
            .HasForeignKey(reward => new { reward.HouseholdId, reward.CreatedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reward => reward.UpdatedByMember).WithMany()
            .HasForeignKey(reward => new { reward.HouseholdId, reward.UpdatedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
