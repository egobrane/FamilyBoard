using FamilyDashboard.Api.Domain.Chores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ChoreDefinitionEntityConfiguration : IEntityTypeConfiguration<ChoreDefinition>
{
    public void Configure(EntityTypeBuilder<ChoreDefinition> builder)
    {
        builder.ToTable("ChoreDefinitions", table =>
            table.HasCheckConstraint("CK_ChoreDefinitions_DefaultPointValue", "\"DefaultPointValue\" BETWEEN 0 AND 10000"));
        builder.HasKey(chore => chore.Id);
        builder.Property(chore => chore.Title).HasMaxLength(120).IsRequired();
        builder.Property(chore => chore.Description).HasMaxLength(500);
        builder.Property(chore => chore.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(chore => chore.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(chore => chore.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasAlternateKey(chore => new { chore.HouseholdId, chore.Id });
        builder.HasIndex(chore => new { chore.HouseholdId, chore.ClientRequestId }).IsUnique();
        builder.HasIndex(chore => new { chore.HouseholdId, chore.IsActive });
        builder.HasOne(chore => chore.Household)
            .WithMany(household => household.ChoreDefinitions)
            .HasForeignKey(chore => chore.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
