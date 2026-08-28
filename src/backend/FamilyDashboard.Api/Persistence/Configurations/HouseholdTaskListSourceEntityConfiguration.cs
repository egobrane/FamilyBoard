using FamilyDashboard.Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdTaskListSourceEntityConfiguration
    : IEntityTypeConfiguration<HouseholdTaskListSource>
{
    public void Configure(EntityTypeBuilder<HouseholdTaskListSource> builder)
    {
        builder.ToTable("HouseholdTaskListSources");
        builder.HasKey(source => source.Id);
        builder.HasAlternateKey(source => new { source.HouseholdId, source.Id });
        builder.Property(source => source.ExternalTaskListId).HasMaxLength(1024).IsRequired();
        builder.Property(source => source.DisplayNameSnapshot).HasMaxLength(200).IsRequired();
        builder.HasIndex(source => new { source.HouseholdId, source.IsActive });
        builder.HasIndex(source => source.HouseholdId).IsUnique()
            .HasFilter("\"IsWriteTarget\" = TRUE");
        builder.HasIndex(source => new { source.GoogleTasksConnectionId, source.ExternalTaskListId })
            .IsUnique().HasFilter("\"IsWriteTarget\" = TRUE");
        builder.HasIndex(source => new
        {
            source.HouseholdId,
            source.GoogleTasksConnectionId,
            source.ExternalTaskListId,
        }).IsUnique();
        builder.HasOne(source => source.Household)
            .WithMany(household => household.TaskListSources)
            .HasForeignKey(source => source.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(source => source.GoogleTasksConnection)
            .WithMany(connection => connection.HouseholdSources)
            .HasForeignKey(source => new
            {
                source.GoogleTasksConnectionId,
                source.OwnerUserAccountId,
            })
            .HasPrincipalKey(connection => new { connection.Id, connection.UserAccountId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(source => source.AddedByUserAccount)
            .WithMany(account => account.AddedHouseholdTaskListSources)
            .HasForeignKey(source => source.AddedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(source => source.WriteTargetConfiguredByUserAccount)
            .WithMany(account => account.ConfiguredWritableTaskListSources)
            .HasForeignKey(source => source.WriteTargetConfiguredByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
