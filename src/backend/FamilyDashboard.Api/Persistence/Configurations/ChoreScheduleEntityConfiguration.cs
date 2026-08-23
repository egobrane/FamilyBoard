using FamilyDashboard.Api.Domain.Chores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ChoreScheduleEntityConfiguration : IEntityTypeConfiguration<ChoreSchedule>
{
    public void Configure(EntityTypeBuilder<ChoreSchedule> builder)
    {
        builder.ToTable("ChoreSchedules", table =>
        {
            table.HasCheckConstraint("CK_ChoreSchedules_Interval", "\"Interval\" >= 1");
            table.HasCheckConstraint("CK_ChoreSchedules_DateRange",
                "\"EndLocalDate\" IS NULL OR \"EndLocalDate\" >= \"StartLocalDate\"");
            table.HasCheckConstraint("CK_ChoreSchedules_WeekdayMask",
                "(\"RecurrenceKind\" = 'Daily' AND \"DaysOfWeekMask\" IS NULL) OR " +
                "(\"RecurrenceKind\" = 'Weekly' AND \"DaysOfWeekMask\" BETWEEN 1 AND 127)");
        });
        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.RecurrenceKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(schedule => schedule.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(schedule => schedule.BlockedReason).HasMaxLength(64);
        builder.Property(schedule => schedule.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(schedule => schedule.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(schedule => schedule.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasAlternateKey(schedule => new { schedule.HouseholdId, schedule.Id });
        builder.HasIndex(schedule => new { schedule.HouseholdId, schedule.ClientRequestId }).IsUnique();
        builder.HasIndex(schedule => new { schedule.Status, schedule.NextOccurrenceLocalDate });
        builder.HasOne(schedule => schedule.Household)
            .WithMany(household => household.ChoreSchedules)
            .HasForeignKey(schedule => schedule.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(schedule => schedule.ChoreDefinition)
            .WithMany(definition => definition.Schedules)
            .HasForeignKey(schedule => new { schedule.HouseholdId, schedule.ChoreDefinitionId })
            .HasPrincipalKey(definition => new { definition.HouseholdId, definition.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(schedule => schedule.HouseholdMember)
            .WithMany(member => member.ChoreSchedules)
            .HasForeignKey(schedule => new { schedule.HouseholdId, schedule.HouseholdMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(schedule => schedule.CreatedByMember)
            .WithMany()
            .HasForeignKey(schedule => new { schedule.HouseholdId, schedule.CreatedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
