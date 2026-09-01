using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdMemberEntityConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable("HouseholdMembers", table =>
        {
            table.HasCheckConstraint("CK_HouseholdMembers_PhotoFocalX", "\"PhotoFocalX\" BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_HouseholdMembers_PhotoFocalY", "\"PhotoFocalY\" BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_HouseholdMembers_PhotoVersion", "\"PhotoVersion\" > 0");
        });
        builder.HasKey(member => member.Id);
        builder.Property(member => member.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(16);
        builder.Property(member => member.AvatarColor).HasMaxLength(20);
        builder.Property(member => member.PhotoFocalX).HasPrecision(5, 4);
        builder.Property(member => member.PhotoFocalY).HasPrecision(5, 4);
        builder.Property(member => member.PhotoVersion).IsConcurrencyToken();
        builder.Property(member => member.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(member => member.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(member => new { member.HouseholdId, member.DisplayName });
        builder.HasAlternateKey(member => new { member.HouseholdId, member.Id });
        builder.HasOne(member => member.Household)
            .WithMany(household => household.Members)
            .HasForeignKey(member => member.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.CurrentPhotoAsset).WithMany()
            .HasForeignKey(member => new { member.HouseholdId, member.Id, member.CurrentPhotoAssetId })
            .HasPrincipalKey(asset => new { asset.HouseholdId, asset.HouseholdMemberId, asset.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
