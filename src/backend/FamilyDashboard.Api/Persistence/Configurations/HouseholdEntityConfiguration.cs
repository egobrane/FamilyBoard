using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdEntityConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Households");
        builder.HasKey(household => household.Id);
        builder.Property(household => household.Name).HasMaxLength(120).IsRequired();
        builder.Property(household => household.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(household => household.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
