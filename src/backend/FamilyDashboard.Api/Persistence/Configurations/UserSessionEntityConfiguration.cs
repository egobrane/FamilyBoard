using FamilyDashboard.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class UserSessionEntityConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions", table =>
        {
            table.HasCheckConstraint("CK_UserSessions_ExpiresAfterCreation", "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CK_UserSessions_AbsoluteExpiration", "\"AbsoluteExpiresAt\" >= \"ExpiresAt\"");
            table.HasCheckConstraint("CK_UserSessions_ParentAccessFailures", "\"ParentAccessFailedAttemptCount\" >= 0");
            table.HasCheckConstraint(
                "CK_UserSessions_AdministrativeElevation",
                "(\"AdministrativeElevationHouseholdId\" IS NULL AND \"AdministrativeElevationExpiresAt\" IS NULL) OR (\"AdministrativeElevationHouseholdId\" IS NOT NULL AND \"AdministrativeElevationExpiresAt\" IS NOT NULL)");
        });
        builder.HasKey(session => session.Id);
        builder.Property(session => session.DeviceLabel).HasMaxLength(80);
        builder.Property(session => session.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(session => new { session.UserAccountId, session.RevokedAt, session.ExpiresAt });
        builder.HasIndex(session => session.AbsoluteExpiresAt);
        builder.HasOne(session => session.UserAccount)
            .WithMany(account => account.UserSessions)
            .HasForeignKey(session => session.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(session => session.SelectedHouseholdMembership)
            .WithMany()
            .HasForeignKey(session => new
            {
                session.UserAccountId,
                session.SelectedHouseholdId,
            })
            .HasPrincipalKey(membership => new
            {
                membership.UserAccountId,
                membership.HouseholdId,
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(session => session.AdministrativeElevationHouseholdMembership)
            .WithMany()
            .HasForeignKey(session => new
            {
                session.UserAccountId,
                session.AdministrativeElevationHouseholdId,
            })
            .HasPrincipalKey(membership => new
            {
                membership.UserAccountId,
                membership.HouseholdId,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
