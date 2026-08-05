using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdConfigurationEntityConfiguration : IEntityTypeConfiguration<HouseholdConfiguration>
{
    public void Configure(EntityTypeBuilder<HouseholdConfiguration> builder)
    {
        builder.ToTable("HouseholdConfigurations");
        builder.HasKey(configuration => configuration.HouseholdId);
        builder.Property(configuration => configuration.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(configuration => configuration.Locale).HasMaxLength(20).IsRequired();
        builder.Property(configuration => configuration.WeekStartsOn)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(configuration => configuration.Theme).HasMaxLength(30).IsRequired();
        builder.Property(configuration => configuration.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne(configuration => configuration.Household)
            .WithOne(household => household.Configuration)
            .HasForeignKey<HouseholdConfiguration>(configuration => configuration.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
