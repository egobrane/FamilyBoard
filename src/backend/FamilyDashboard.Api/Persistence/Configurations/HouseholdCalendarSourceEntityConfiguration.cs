using FamilyDashboard.Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdCalendarSourceEntityConfiguration
    : IEntityTypeConfiguration<HouseholdCalendarSource>
{
    public void Configure(EntityTypeBuilder<HouseholdCalendarSource> builder)
    {
        builder.ToTable("HouseholdCalendarSources", table => table.HasCheckConstraint(
            "CK_HouseholdCalendarSources_EventCreationTarget",
            "(\"IsEventCreationTarget\" AND \"IsActive\" AND \"EventCreationEnabledAt\" IS NOT NULL AND \"EventCreationEnabledByUserAccountId\" IS NOT NULL) OR (NOT \"IsEventCreationTarget\" AND \"EventCreationEnabledAt\" IS NULL AND \"EventCreationEnabledByUserAccountId\" IS NULL)"));
        builder.HasKey(source => source.Id);
        builder.HasAlternateKey(source => new { source.HouseholdId, source.Id });
        builder.Property(source => source.ExternalCalendarId).HasMaxLength(1024).IsRequired();
        builder.Property(source => source.DisplayNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(source => source.TimeZone).HasMaxLength(100);
        builder.Property(source => source.Color).HasMaxLength(32);
        builder.HasIndex(source => new { source.HouseholdId, source.IsActive });
        builder.HasIndex(source => source.HouseholdId)
            .IsUnique()
            .HasFilter("\"IsActive\" AND \"IsEventCreationTarget\"");
        builder.HasIndex(source => new
        {
            source.HouseholdId,
            source.GoogleCalendarConnectionId,
            source.ExternalCalendarId,
        }).IsUnique();
        builder.HasOne(source => source.Household)
            .WithMany(household => household.CalendarSources)
            .HasForeignKey(source => source.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(source => source.GoogleCalendarConnection)
            .WithMany(connection => connection.HouseholdSources)
            .HasForeignKey(source => new
            {
                source.GoogleCalendarConnectionId,
                source.OwnerUserAccountId,
            })
            .HasPrincipalKey(connection => new { connection.Id, connection.UserAccountId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(source => source.AddedByUserAccount)
            .WithMany(account => account.AddedHouseholdCalendarSources)
            .HasForeignKey(source => source.AddedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(source => source.EventCreationEnabledByUserAccount)
            .WithMany(account => account.EnabledCalendarEventCreationSources)
            .HasForeignKey(source => source.EventCreationEnabledByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
