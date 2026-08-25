using FamilyDashboard.Api.Domain.Chores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ChoreAssignmentEntityConfiguration : IEntityTypeConfiguration<ChoreAssignment>
{
    public void Configure(EntityTypeBuilder<ChoreAssignment> builder)
    {
        builder.ToTable("ChoreAssignments", table =>
            table.HasCheckConstraint("CK_ChoreAssignments_PointValueSnapshot", "\"PointValueSnapshot\" BETWEEN 0 AND 10000"));
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(assignment => assignment.DueTimeResolution).HasConversion<string>().HasMaxLength(24)
            .HasDefaultValue(ChoreDueTimeResolution.Exact);
        builder.Property(assignment => assignment.TitleSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(assignment => assignment.DescriptionSnapshot).HasMaxLength(500);
        builder.Property(assignment => assignment.DueTimeZone).HasMaxLength(64);
        builder.Property(assignment => assignment.SkipReason).HasMaxLength(240);
        builder.Property(assignment => assignment.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(assignment => assignment.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(assignment => assignment.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasAlternateKey(assignment => new { assignment.HouseholdId, assignment.Id });
        builder.HasIndex(assignment => new { assignment.HouseholdId, assignment.ClientRequestId }).IsUnique();
        builder.HasIndex(assignment => new { assignment.HouseholdId, assignment.ChoreScheduleId,
            assignment.ScheduleOccurrenceLocalDate }).IsUnique()
            .HasFilter("\"ChoreScheduleId\" IS NOT NULL");
        builder.HasIndex(assignment => new { assignment.HouseholdId, assignment.Status, assignment.DueAt });
        builder.HasIndex(assignment => new { assignment.HouseholdId, assignment.HouseholdMemberId, assignment.Status, assignment.DueAt });
        builder.HasOne(assignment => assignment.ChoreDefinition)
            .WithMany(chore => chore.Assignments)
            .HasForeignKey(assignment => new { assignment.HouseholdId, assignment.ChoreDefinitionId })
            .HasPrincipalKey(chore => new { chore.HouseholdId, chore.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.HouseholdMember)
            .WithMany(member => member.ChoreAssignments)
            .HasForeignKey(assignment => new { assignment.HouseholdId, assignment.HouseholdMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.CreatedByMember)
            .WithMany()
            .HasForeignKey(assignment => new { assignment.HouseholdId, assignment.CreatedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.SkippedByMember)
            .WithMany()
            .HasForeignKey(assignment => new { assignment.HouseholdId, assignment.SkippedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.ChoreSchedule)
            .WithMany(schedule => schedule.Assignments)
            .HasForeignKey(assignment => new { assignment.HouseholdId, assignment.ChoreScheduleId })
            .HasPrincipalKey(schedule => new { schedule.HouseholdId, schedule.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
