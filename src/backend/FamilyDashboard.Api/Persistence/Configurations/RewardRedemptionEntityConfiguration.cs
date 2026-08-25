using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class RewardRedemptionEntityConfiguration : IEntityTypeConfiguration<RewardRedemption>
{
    public void Configure(EntityTypeBuilder<RewardRedemption> builder)
    {
        builder.ToTable("RewardRedemptions", table =>
        {
            table.HasCheckConstraint("CK_RewardRedemptions_PointCostSnapshot", "\"PointCostSnapshot\" BETWEEN 1 AND 10000");
            table.HasCheckConstraint("CK_RewardRedemptions_Version", "\"Version\" > 0");
            table.HasCheckConstraint("CK_RewardRedemptions_ClientRequestId", "\"ClientRequestId\" <> '00000000-0000-0000-0000-000000000000'");
        });
        builder.HasKey(redemption => redemption.Id);
        builder.HasAlternateKey(redemption => new { redemption.HouseholdId, redemption.Id });
        builder.Property(redemption => redemption.RewardTitleSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(redemption => redemption.RewardDescriptionSnapshot).HasMaxLength(500);
        builder.Property(redemption => redemption.ReviewNote).HasMaxLength(240);
        builder.Property(redemption => redemption.CancellationReason).HasMaxLength(240);
        builder.Property(redemption => redemption.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.Property(redemption => redemption.Status).HasConversion<string>().HasMaxLength(24);
        builder.HasIndex(redemption => new { redemption.HouseholdId, redemption.HouseholdMemberId, redemption.RequestedAt });
        builder.HasIndex(redemption => new { redemption.HouseholdId, redemption.Status, redemption.RequestedAt });
        builder.HasIndex(redemption => new { redemption.HouseholdId, redemption.ClientRequestId }).IsUnique();
        builder.HasOne(redemption => redemption.Reward)
            .WithMany(reward => reward.Redemptions)
            .HasForeignKey(redemption => new { redemption.HouseholdId, redemption.RewardId })
            .HasPrincipalKey(reward => new { reward.HouseholdId, reward.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.HouseholdMember)
            .WithMany(member => member.RewardRedemptions)
            .HasForeignKey(redemption => new { redemption.HouseholdId, redemption.HouseholdMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.ReviewedByMember)
            .WithMany()
            .HasForeignKey(redemption => new { redemption.HouseholdId, redemption.ReviewedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.RequestedByMember).WithMany()
            .HasForeignKey(redemption => new { redemption.HouseholdId, redemption.RequestedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.FulfilledByMember).WithMany()
            .HasForeignKey(redemption => new { redemption.HouseholdId, redemption.FulfilledByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.CancelledByMember).WithMany()
            .HasForeignKey(redemption => new { redemption.HouseholdId, redemption.CancelledByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(redemption => redemption.RequestedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
