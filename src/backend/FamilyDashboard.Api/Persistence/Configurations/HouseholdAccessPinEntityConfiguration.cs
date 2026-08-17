using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdAccessPinEntityConfiguration : IEntityTypeConfiguration<HouseholdAccessPin>
{
    public void Configure(EntityTypeBuilder<HouseholdAccessPin> builder)
    {
        builder.ToTable("HouseholdAccessPins", table =>
        {
            table.HasCheckConstraint("CK_HouseholdAccessPins_HashLength", "octet_length(\"PinHash\") = 32");
            table.HasCheckConstraint("CK_HouseholdAccessPins_SaltLength", "octet_length(\"Salt\") = 16");
            table.HasCheckConstraint("CK_HouseholdAccessPins_Versions", "\"HashVersion\" > 0 AND \"PepperVersion\" > 0 AND \"WorkFactor\" > 0");
            table.HasCheckConstraint("CK_HouseholdAccessPins_ChangedAfterCreated", "\"ChangedAt\" >= \"CreatedAt\"");
        });
        builder.HasKey(pin => pin.HouseholdId);
        builder.Property(pin => pin.PinHash).HasColumnType("bytea").IsRequired();
        builder.Property(pin => pin.Salt).HasColumnType("bytea").IsRequired();
        builder.Property(pin => pin.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne(pin => pin.Household)
            .WithOne(household => household.AccessPin)
            .HasForeignKey<HouseholdAccessPin>(pin => pin.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(pin => pin.ChangedByUserAccount)
            .WithMany(account => account.ChangedHouseholdAccessPins)
            .HasForeignKey(pin => pin.ChangedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
