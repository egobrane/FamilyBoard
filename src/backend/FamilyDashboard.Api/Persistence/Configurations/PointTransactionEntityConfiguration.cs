using FamilyDashboard.Api.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class PointTransactionEntityConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.ToTable("PointTransactions", table =>
            table.HasCheckConstraint("CK_PointTransactions_Amount", "\"Amount\" <> 0"));
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(24);
        builder.Property(transaction => transaction.Description).HasMaxLength(250).IsRequired();
        builder.Property(transaction => transaction.IdempotencyKey).HasMaxLength(120);
        builder.HasIndex(transaction => new { transaction.HouseholdMemberId, transaction.CreatedAt });
        builder.HasIndex(transaction => transaction.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasIndex(transaction => transaction.ChoreCompletionId).IsUnique();
        builder.HasIndex(transaction => transaction.RewardRedemptionId).IsUnique();
        builder.HasOne(transaction => transaction.Household)
            .WithMany()
            .HasForeignKey(transaction => transaction.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.HouseholdMember)
            .WithMany(member => member.PointTransactions)
            .HasForeignKey(transaction => transaction.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.ChoreCompletion)
            .WithOne(completion => completion.PointTransaction)
            .HasForeignKey<PointTransaction>(transaction => transaction.ChoreCompletionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.RewardRedemption)
            .WithOne(redemption => redemption.PointTransaction)
            .HasForeignKey<PointTransaction>(transaction => transaction.RewardRedemptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
