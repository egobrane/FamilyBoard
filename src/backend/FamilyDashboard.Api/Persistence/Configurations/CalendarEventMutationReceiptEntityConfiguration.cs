using FamilyDashboard.Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class CalendarEventMutationReceiptEntityConfiguration
    : IEntityTypeConfiguration<CalendarEventMutationReceipt>
{
    public void Configure(EntityTypeBuilder<CalendarEventMutationReceipt> builder)
    {
        builder.ToTable("CalendarEventMutationReceipts", table =>
        {
            table.HasCheckConstraint("CK_CalendarEventMutationReceipts_Fingerprint",
                "octet_length(\"RequestFingerprint\") = 32");
            table.HasCheckConstraint("CK_CalendarEventMutationReceipts_Completion",
                "(\"Status\" = 'Pending' AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Succeeded' AND \"CompletedAt\" IS NOT NULL)");
        });
        builder.HasKey(receipt => new { receipt.HouseholdId, receipt.Id });
        builder.Property(receipt => receipt.Operation).HasConversion<string>().HasMaxLength(16);
        builder.Property(receipt => receipt.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(receipt => receipt.RequestFingerprint).HasColumnType("bytea").IsRequired();
        builder.Property(receipt => receipt.ExpectedProviderVersion).HasMaxLength(512).IsRequired();
        builder.Property(receipt => receipt.ResultProviderVersion).HasMaxLength(512);
        builder.Property(receipt => receipt.TraceId).HasMaxLength(128).IsRequired();
        builder.HasIndex(receipt => new { receipt.HouseholdId, receipt.CalendarEventCreationReceiptId, receipt.CreatedAt });
        builder.HasOne(receipt => receipt.Household).WithMany(household => household.CalendarEventMutationReceipts)
            .HasForeignKey(receipt => receipt.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.CalendarEventCreationReceipt)
            .WithMany(creation => creation.MutationReceipts)
            .HasForeignKey(receipt => new { receipt.HouseholdId, receipt.CalendarEventCreationReceiptId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.HouseholdCalendarSource)
            .WithMany(source => source.EventMutationReceipts)
            .HasForeignKey(receipt => new { receipt.HouseholdId, receipt.HouseholdCalendarSourceId })
            .HasPrincipalKey(source => new { source.HouseholdId, source.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.RequestedByUserAccount)
            .WithMany(account => account.RequestedCalendarEventMutations)
            .HasForeignKey(receipt => receipt.RequestedByUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.ActingHouseholdMember)
            .WithMany(member => member.ActedCalendarEventMutations)
            .HasForeignKey(receipt => new { receipt.HouseholdId, receipt.ActingHouseholdMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
