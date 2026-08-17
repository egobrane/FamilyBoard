using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ParentAccessAuditEventEntityConfiguration : IEntityTypeConfiguration<ParentAccessAuditEvent>
{
    public void Configure(EntityTypeBuilder<ParentAccessAuditEvent> builder)
    {
        builder.ToTable("ParentAccessAuditEvents");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.EventType).HasConversion<string>().HasMaxLength(40);
        builder.Property(auditEvent => auditEvent.Outcome).HasConversion<string>().HasMaxLength(16);
        builder.Property(auditEvent => auditEvent.TraceId).HasMaxLength(100);
        builder.Property(auditEvent => auditEvent.OccurredAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(auditEvent => new { auditEvent.HouseholdId, auditEvent.OccurredAt });
        builder.HasIndex(auditEvent => new { auditEvent.UserSessionId, auditEvent.OccurredAt });
        builder.HasOne(auditEvent => auditEvent.Household)
            .WithMany(household => household.ParentAccessAuditEvents)
            .HasForeignKey(auditEvent => auditEvent.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(auditEvent => auditEvent.UserAccount)
            .WithMany(account => account.ParentAccessAuditEvents)
            .HasForeignKey(auditEvent => auditEvent.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(auditEvent => auditEvent.UserSession)
            .WithMany(session => session.ParentAccessAuditEvents)
            .HasForeignKey(auditEvent => auditEvent.UserSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
