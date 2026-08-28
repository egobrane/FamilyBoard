using FamilyDashboard.Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class GoogleTaskMutationReceiptEntityConfiguration
    : IEntityTypeConfiguration<GoogleTaskMutationReceipt>
{
    public void Configure(EntityTypeBuilder<GoogleTaskMutationReceipt> builder)
    {
        builder.ToTable("GoogleTaskMutationReceipts", table =>
        {
            table.HasCheckConstraint("CK_GoogleTaskMutationReceipts_Fingerprint",
                "octet_length(\"RequestFingerprint\") = 32");
            table.HasCheckConstraint("CK_GoogleTaskMutationReceipts_Completion",
                "(\"Status\" = 'Pending' AND \"CompletedAt\" IS NULL) OR (\"Status\" <> 'Pending' AND \"CompletedAt\" IS NOT NULL)");
        });
        builder.HasKey(receipt => new { receipt.HouseholdId, receipt.Id });
        builder.Property(receipt => receipt.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(receipt => receipt.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(receipt => receipt.RequestFingerprint).HasColumnType("bytea").IsRequired();
        builder.Property(receipt => receipt.ProviderTaskId).HasMaxLength(1024);
        builder.Property(receipt => receipt.ResultProviderETag).HasMaxLength(512);
        builder.Property(receipt => receipt.FailureCode).HasMaxLength(80);
        builder.Property(receipt => receipt.TraceId).HasMaxLength(128).IsRequired();
        builder.HasIndex(receipt => new { receipt.HouseholdId, receipt.HouseholdTaskListSourceId, receipt.CreatedAt });
        builder.HasOne(receipt => receipt.Household)
            .WithMany(household => household.GoogleTaskMutationReceipts)
            .HasForeignKey(receipt => receipt.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.HouseholdTaskListSource)
            .WithMany(source => source.MutationReceipts)
            .HasForeignKey(receipt => new { receipt.HouseholdId, receipt.HouseholdTaskListSourceId })
            .HasPrincipalKey(source => new { source.HouseholdId, source.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.GoogleTasksConnection)
            .WithMany(connection => connection.MutationReceipts)
            .HasForeignKey(receipt => receipt.GoogleTasksConnectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.RequestedByUserAccount)
            .WithMany(account => account.RequestedGoogleTaskMutations)
            .HasForeignKey(receipt => receipt.RequestedByUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.AttributedHouseholdMember)
            .WithMany(member => member.AttributedGoogleTaskMutations)
            .HasForeignKey(receipt => new { receipt.HouseholdId, receipt.AttributedHouseholdMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
