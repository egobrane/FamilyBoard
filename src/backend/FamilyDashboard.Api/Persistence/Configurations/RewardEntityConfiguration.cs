using FamilyDashboard.Api.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class RewardEntityConfiguration : IEntityTypeConfiguration<Reward>
{
    public void Configure(EntityTypeBuilder<Reward> builder)
    {
        builder.ToTable("Rewards", table =>
            table.HasCheckConstraint("CK_Rewards_PointCost", "\"PointCost\" > 0"));
        builder.HasKey(reward => reward.Id);
        builder.Property(reward => reward.Title).HasMaxLength(120).IsRequired();
        builder.Property(reward => reward.Description).HasMaxLength(500);
        builder.Property(reward => reward.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(reward => reward.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(reward => new { reward.HouseholdId, reward.IsActive });
        builder.HasOne(reward => reward.Household)
            .WithMany(household => household.Rewards)
            .HasForeignKey(reward => reward.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
