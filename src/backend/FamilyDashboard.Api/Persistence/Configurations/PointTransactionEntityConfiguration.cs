using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Domain.Chores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class PointTransactionEntityConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.ToTable("PointTransactions", table =>
        {
            table.HasCheckConstraint("CK_PointTransactions_Amount", "\"Amount\" <> 0");
            table.HasCheckConstraint("CK_PointTransactions_ReversalLink",
                "(\"Type\" = 'Reversal' AND \"ReversesPointTransactionId\" IS NOT NULL) OR (\"Type\" <> 'Reversal' AND \"ReversesPointTransactionId\" IS NULL)");
            table.HasCheckConstraint("CK_PointTransactions_ChoreCompletionLink",
                "\"Type\" <> 'ChoreCompletion' OR (\"ChoreCompletionId\" IS NOT NULL AND \"Amount\" > 0)");
            table.HasCheckConstraint("CK_PointTransactions_RewardRedemptionLink",
                "\"Type\" <> 'RewardRedemption' OR (\"RewardRedemptionId\" IS NOT NULL AND \"Amount\" < 0)");
        });
        builder.HasKey(transaction => transaction.Id);
        builder.HasAlternateKey(transaction => new { transaction.HouseholdId, transaction.Id });
        builder.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(24);
        builder.Property(transaction => transaction.Description).HasMaxLength(250).IsRequired();
        builder.Property(transaction => transaction.IdempotencyKey).HasMaxLength(120);
        builder.HasIndex(transaction => new { transaction.HouseholdId, transaction.HouseholdMemberId,
            transaction.CreatedAt, transaction.Id });
        builder.HasIndex(transaction => new { transaction.HouseholdId, transaction.IdempotencyKey })
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
            .HasForeignKey(transaction => new { transaction.HouseholdId, transaction.HouseholdMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.CreatedByMember)
            .WithMany(member => member.CreatedPointTransactions)
            .HasForeignKey(transaction => new { transaction.HouseholdId, transaction.CreatedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.ChoreCompletion)
            .WithOne(completion => completion.PointTransaction)
            .HasForeignKey<PointTransaction>(transaction => new { transaction.HouseholdId, transaction.ChoreCompletionId })
            .HasPrincipalKey<ChoreCompletion>(completion => new { completion.HouseholdId, completion.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.RewardRedemption)
            .WithOne(redemption => redemption.PointTransaction)
            .HasForeignKey<PointTransaction>(transaction => new { transaction.HouseholdId, transaction.RewardRedemptionId })
            .HasPrincipalKey<RewardRedemption>(redemption => new { redemption.HouseholdId, redemption.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.ReversesPointTransaction)
            .WithOne(transaction => transaction.ReversalTransaction)
            .HasForeignKey<PointTransaction>(transaction => new { transaction.HouseholdId, transaction.ReversesPointTransactionId })
            .HasPrincipalKey<PointTransaction>(transaction => new { transaction.HouseholdId, transaction.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
