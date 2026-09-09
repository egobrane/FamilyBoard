using FamilyDashboard.Api.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class PointDeductionDefinitionEntityConfiguration
    : IEntityTypeConfiguration<PointDeductionDefinition>
{
    public void Configure(EntityTypeBuilder<PointDeductionDefinition> builder)
    {
        builder.ToTable("PointDeductionDefinitions", table =>
        {
            table.HasCheckConstraint("CK_PointDeductionDefinitions_Amount",
                "\"DeductionAmount\" BETWEEN 1 AND 10000");
            table.HasCheckConstraint("CK_PointDeductionDefinitions_Version", "\"Version\" > 0");
            table.HasCheckConstraint("CK_PointDeductionDefinitions_ClientRequestId",
                "\"ClientRequestId\" <> '00000000-0000-0000-0000-000000000000'");
        });
        builder.HasKey(definition => definition.Id);
        builder.HasAlternateKey(definition => new { definition.HouseholdId, definition.Id });
        builder.Property(definition => definition.Title).HasMaxLength(120).IsRequired();
        builder.Property(definition => definition.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.Property(definition => definition.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(definition => definition.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(definition => new { definition.HouseholdId, definition.IsActive,
            definition.Title });
        builder.HasIndex(definition => new { definition.HouseholdId, definition.ClientRequestId })
            .IsUnique();
        builder.HasOne(definition => definition.Household)
            .WithMany(household => household.PointDeductionDefinitions)
            .HasForeignKey(definition => definition.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(definition => definition.CreatedByMember).WithMany()
            .HasForeignKey(definition => new { definition.HouseholdId, definition.CreatedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(definition => definition.UpdatedByMember).WithMany()
            .HasForeignKey(definition => new { definition.HouseholdId, definition.UpdatedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
