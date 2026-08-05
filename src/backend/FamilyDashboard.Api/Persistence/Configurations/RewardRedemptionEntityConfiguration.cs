using FamilyDashboard.Api.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class RewardRedemptionEntityConfiguration : IEntityTypeConfiguration<RewardRedemption>
{
    public void Configure(EntityTypeBuilder<RewardRedemption> builder)
    {
        builder.ToTable("RewardRedemptions", table =>
            table.HasCheckConstraint("CK_RewardRedemptions_PointCostSnapshot", "\"PointCostSnapshot\" > 0"));
        builder.HasKey(redemption => redemption.Id);
        builder.Property(redemption => redemption.Status).HasConversion<string>().HasMaxLength(24);
        builder.HasIndex(redemption => new { redemption.HouseholdMemberId, redemption.RequestedAt });
        builder.HasOne(redemption => redemption.Reward)
            .WithMany(reward => reward.Redemptions)
            .HasForeignKey(redemption => redemption.RewardId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.HouseholdMember)
            .WithMany(member => member.RewardRedemptions)
            .HasForeignKey(redemption => redemption.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(redemption => redemption.ReviewedByMember)
            .WithMany()
            .HasForeignKey(redemption => redemption.ReviewedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
