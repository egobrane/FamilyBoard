using FamilyDashboard.Api.Domain.Chores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ChoreAssignmentEntityConfiguration : IEntityTypeConfiguration<ChoreAssignment>
{
    public void Configure(EntityTypeBuilder<ChoreAssignment> builder)
    {
        builder.ToTable("ChoreAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(assignment => assignment.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(assignment => assignment.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(assignment => new { assignment.HouseholdMemberId, assignment.DueAt });
        builder.HasOne(assignment => assignment.ChoreDefinition)
            .WithMany(chore => chore.Assignments)
            .HasForeignKey(assignment => assignment.ChoreDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.HouseholdMember)
            .WithMany(member => member.ChoreAssignments)
            .HasForeignKey(assignment => assignment.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
