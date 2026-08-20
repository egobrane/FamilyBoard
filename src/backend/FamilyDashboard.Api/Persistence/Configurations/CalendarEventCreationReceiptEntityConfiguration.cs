using FamilyDashboard.Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class CalendarEventCreationReceiptEntityConfiguration
    : IEntityTypeConfiguration<CalendarEventCreationReceipt>
{
    public void Configure(EntityTypeBuilder<CalendarEventCreationReceipt> builder)
    {
        builder.ToTable("CalendarEventCreationReceipts", table =>
        {
            table.HasCheckConstraint(
                "CK_CalendarEventCreationReceipts_Fingerprint",
                "octet_length(\"RequestFingerprint\") = 32");
            table.HasCheckConstraint(
                "CK_CalendarEventCreationReceipts_Completion",
                "(\"Status\" = 'Pending' AND \"ProviderEventId\" IS NULL AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Succeeded' AND \"ProviderEventId\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL)");
        });
        builder.HasKey(receipt => new { receipt.HouseholdId, receipt.Id });
        builder.Property(receipt => receipt.RequestFingerprint).HasColumnType("bytea").IsRequired();
        builder.Property(receipt => receipt.ProviderEventId).HasMaxLength(1024);
        builder.Property(receipt => receipt.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(receipt => receipt.TraceId).HasMaxLength(128).IsRequired();
        builder.HasIndex(receipt => new { receipt.HouseholdId, receipt.Status, receipt.CreatedAt });
        builder.HasIndex(receipt => new
        {
            receipt.HouseholdCalendarSourceId,
            receipt.ProviderEventId,
        }).IsUnique().HasFilter("\"ProviderEventId\" IS NOT NULL");
        builder.HasOne(receipt => receipt.Household)
            .WithMany(household => household.CalendarEventCreationReceipts)
            .HasForeignKey(receipt => receipt.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.HouseholdCalendarSource)
            .WithMany(source => source.EventCreationReceipts)
            .HasForeignKey(receipt => new
            {
                receipt.HouseholdId,
                receipt.HouseholdCalendarSourceId,
            })
            .HasPrincipalKey(source => new { source.HouseholdId, source.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.RequestedByUserAccount)
            .WithMany(account => account.RequestedCalendarEventCreations)
            .HasForeignKey(receipt => receipt.RequestedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.AttributedHouseholdMember)
            .WithMany(member => member.AttributedCalendarEventCreations)
            .HasForeignKey(receipt => new
            {
                receipt.HouseholdId,
                receipt.AttributedHouseholdMemberId,
            })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
