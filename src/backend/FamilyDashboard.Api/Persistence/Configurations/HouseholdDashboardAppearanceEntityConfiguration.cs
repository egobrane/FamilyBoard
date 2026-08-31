using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdDashboardAppearanceEntityConfiguration : IEntityTypeConfiguration<HouseholdDashboardAppearance>
{
    public void Configure(EntityTypeBuilder<HouseholdDashboardAppearance> builder)
    {
        builder.ToTable("HouseholdDashboardAppearances", table =>
        {
            table.HasCheckConstraint("CK_HouseholdDashboardAppearances_FocalX", "\"PhotoFocalX\" BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_HouseholdDashboardAppearances_FocalY", "\"PhotoFocalY\" BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_HouseholdDashboardAppearances_Version", "\"Version\" > 0");
        });
        builder.HasKey(value => value.HouseholdId);
        builder.Property(value => value.GreetingTitle).HasMaxLength(80);
        builder.Property(value => value.GreetingMessage).HasMaxLength(240);
        builder.Property(value => value.PhotoFocalX).HasPrecision(5, 4);
        builder.Property(value => value.PhotoFocalY).HasPrecision(5, 4);
        builder.Property(value => value.Version).IsConcurrencyToken();
        builder.Property(value => value.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne(value => value.Household).WithOne(value => value.DashboardAppearance)
            .HasForeignKey<HouseholdDashboardAppearance>(value => value.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.CurrentPhotoAsset).WithMany()
            .HasForeignKey(value => new { value.HouseholdId, value.CurrentPhotoAssetId })
            .HasPrincipalKey(value => new { value.HouseholdId, value.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
