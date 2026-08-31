using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdPhotoAssetEntityConfiguration : IEntityTypeConfiguration<HouseholdPhotoAsset>
{
    public void Configure(EntityTypeBuilder<HouseholdPhotoAsset> builder)
    {
        builder.ToTable("HouseholdPhotoAssets", table =>
        {
            table.HasCheckConstraint("CK_HouseholdPhotoAssets_Dimensions", "\"PixelWidth\" > 0 AND \"PixelHeight\" > 0");
            table.HasCheckConstraint("CK_HouseholdPhotoAssets_Length", "\"TotalByteLength\" > 0");
            table.HasCheckConstraint("CK_HouseholdPhotoAssets_Retirement", "\"RetiredAt\" IS NULL OR \"RetiredAt\" >= \"CreatedAt\"");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.StoragePrefix).HasMaxLength(240).IsRequired();
        builder.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasAlternateKey(value => new { value.HouseholdId, value.Id });
        builder.HasIndex(value => new { value.HouseholdId, value.RetiredAt });
        builder.HasOne(value => value.Household).WithMany(value => value.PhotoAssets)
            .HasForeignKey(value => value.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.CreatedByHouseholdMember).WithMany()
            .HasForeignKey(value => new { value.HouseholdId, value.CreatedByHouseholdMemberId })
            .HasPrincipalKey(value => new { value.HouseholdId, value.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
