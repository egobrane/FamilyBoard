using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdWeatherConfigurationEntityConfiguration : IEntityTypeConfiguration<HouseholdWeatherConfiguration>
{
    public void Configure(EntityTypeBuilder<HouseholdWeatherConfiguration> builder)
    {
        builder.ToTable("HouseholdWeatherConfigurations", table =>
        {
            table.HasCheckConstraint("CK_HouseholdWeatherConfigurations_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
            table.HasCheckConstraint("CK_HouseholdWeatherConfigurations_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
            table.HasCheckConstraint("CK_HouseholdWeatherConfigurations_TemperatureUnit", "\"TemperatureUnit\" IN ('auto', 'fahrenheit', 'celsius')");
            table.HasCheckConstraint("CK_HouseholdWeatherConfigurations_Version", "\"Version\" > 0");
        });
        builder.HasKey(value => value.HouseholdId);
        builder.Property(value => value.Latitude).HasPrecision(8, 5);
        builder.Property(value => value.Longitude).HasPrecision(8, 5);
        builder.Property(value => value.LocationLabel).HasMaxLength(100).IsRequired();
        builder.Property(value => value.TemperatureUnit).HasMaxLength(16).IsRequired();
        builder.Property(value => value.Version).IsConcurrencyToken();
        builder.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne(value => value.Household).WithOne(value => value.WeatherConfiguration)
            .HasForeignKey<HouseholdWeatherConfiguration>(value => value.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
