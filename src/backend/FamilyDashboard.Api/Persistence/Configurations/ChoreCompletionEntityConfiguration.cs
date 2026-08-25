using FamilyDashboard.Api.Domain.Chores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ChoreCompletionEntityConfiguration : IEntityTypeConfiguration<ChoreCompletion>
{
    public void Configure(EntityTypeBuilder<ChoreCompletion> builder)
    {
        builder.ToTable("ChoreCompletions", table =>
            table.HasCheckConstraint("CK_ChoreCompletions_PointValueSnapshot", "\"PointValueSnapshot\" BETWEEN 0 AND 10000"));
        builder.HasKey(completion => completion.Id);
        builder.HasAlternateKey(completion => new { completion.HouseholdId, completion.Id });
        builder.Property(completion => completion.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(completion => completion.ReviewNote).HasMaxLength(240);
        builder.Property(completion => completion.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(completion => new { completion.HouseholdId, completion.ClientRequestId }).IsUnique();
        builder.HasIndex(completion => new { completion.HouseholdId, completion.ChoreAssignmentId });
        builder.HasIndex(completion => new { completion.HouseholdId, completion.ChoreAssignmentId })
            .IsUnique()
            .HasFilter("\"Status\" = 'PendingReview'");
        builder.HasOne(completion => completion.ChoreAssignment)
            .WithMany(assignment => assignment.Completions)
            .HasForeignKey(completion => new { completion.HouseholdId, completion.ChoreAssignmentId })
            .HasPrincipalKey(assignment => new { assignment.HouseholdId, assignment.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(completion => completion.CompletedByMember)
            .WithMany()
            .HasForeignKey(completion => new { completion.HouseholdId, completion.CompletedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(completion => completion.ReviewedByMember)
            .WithMany()
            .HasForeignKey(completion => new { completion.HouseholdId, completion.ReviewedByMemberId })
            .HasPrincipalKey(member => new { member.HouseholdId, member.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FamilyDashboard.Api.Domain.Identity.UserAccount>()
            .WithMany()
            .HasForeignKey(completion => completion.SubmittedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
