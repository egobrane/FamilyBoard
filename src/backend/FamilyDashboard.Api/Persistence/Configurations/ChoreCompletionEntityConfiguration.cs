using FamilyDashboard.Api.Domain.Chores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ChoreCompletionEntityConfiguration : IEntityTypeConfiguration<ChoreCompletion>
{
    public void Configure(EntityTypeBuilder<ChoreCompletion> builder)
    {
        builder.ToTable("ChoreCompletions");
        builder.HasKey(completion => completion.Id);
        builder.Property(completion => completion.Status).HasConversion<string>().HasMaxLength(24);
        builder.HasIndex(completion => completion.ChoreAssignmentId).IsUnique();
        builder.HasOne(completion => completion.ChoreAssignment)
            .WithOne(assignment => assignment.Completion)
            .HasForeignKey<ChoreCompletion>(completion => completion.ChoreAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(completion => completion.CompletedByMember)
            .WithMany()
            .HasForeignKey(completion => completion.CompletedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(completion => completion.ReviewedByMember)
            .WithMany()
            .HasForeignKey(completion => completion.ReviewedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
