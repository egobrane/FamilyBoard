using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdMemberPhotoAssetEntityConfiguration : IEntityTypeConfiguration<HouseholdMemberPhotoAsset>
{
    public void Configure(EntityTypeBuilder<HouseholdMemberPhotoAsset> builder)
    {
        builder.ToTable("HouseholdMemberPhotoAssets", table =>
        {
            table.HasCheckConstraint("CK_HouseholdMemberPhotoAssets_Dimensions", "\"PixelWidth\" > 0 AND \"PixelHeight\" > 0");
            table.HasCheckConstraint("CK_HouseholdMemberPhotoAssets_Length", "\"TotalByteLength\" > 0");
            table.HasCheckConstraint("CK_HouseholdMemberPhotoAssets_Lifecycle",
                "(\"State\" = 'Pending' AND \"ActivatedAt\" IS NULL AND \"RetiredAt\" IS NULL) OR " +
                "(\"State\" = 'Active' AND \"ActivatedAt\" IS NOT NULL AND \"RetiredAt\" IS NULL) OR " +
                "(\"State\" = 'Retired' AND \"RetiredAt\" IS NOT NULL)");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.StoragePrefix).HasMaxLength(240).IsRequired();
        builder.Property(value => value.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(value => value.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasAlternateKey(value => new { value.HouseholdId, value.HouseholdMemberId, value.Id });
        builder.HasIndex(value => value.StoragePrefix).IsUnique();
        builder.HasIndex(value => new { value.HouseholdId, value.HouseholdMemberId })
            .IsUnique().HasFilter("\"State\" = 'Active'");
        builder.HasIndex(value => new { value.State, value.CreatedAt });
        builder.HasOne(value => value.Household).WithMany(value => value.MemberPhotoAssets)
            .HasForeignKey(value => value.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.HouseholdMember).WithMany(value => value.PhotoAssets)
            .HasForeignKey(value => new { value.HouseholdId, value.HouseholdMemberId })
            .HasPrincipalKey(value => new { value.HouseholdId, value.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.CreatedByHouseholdMember).WithMany(value => value.CreatedPhotoAssets)
            .HasForeignKey(value => new { value.HouseholdId, value.CreatedByHouseholdMemberId })
            .HasPrincipalKey(value => new { value.HouseholdId, value.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
