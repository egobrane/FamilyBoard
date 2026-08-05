using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ApplicationPreferenceEntityConfiguration : IEntityTypeConfiguration<ApplicationPreference>
{
    public void Configure(EntityTypeBuilder<ApplicationPreference> builder)
    {
        builder.ToTable("ApplicationPreferences");
        builder.HasKey(preference => preference.Id);
        builder.Property(preference => preference.Key).HasMaxLength(120).IsRequired();
        builder.Property(preference => preference.ValueJson).HasColumnType("jsonb").IsRequired();
        builder.Property(preference => preference.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(preference => new { preference.HouseholdId, preference.Key })
            .IsUnique()
            .HasFilter("\"HouseholdMemberId\" IS NULL");
        builder.HasIndex(preference => new { preference.HouseholdMemberId, preference.Key })
            .IsUnique()
            .HasFilter("\"HouseholdMemberId\" IS NOT NULL");
        builder.HasOne(preference => preference.Household)
            .WithMany(household => household.Preferences)
            .HasForeignKey(preference => preference.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(preference => preference.HouseholdMember)
            .WithMany(member => member.Preferences)
            .HasForeignKey(preference => preference.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
