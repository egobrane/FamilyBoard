using FamilyDashboard.Api.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class HouseholdInvitationEntityConfiguration
    : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
    {
        builder.ToTable("HouseholdInvitations", table =>
        {
            table.HasCheckConstraint(
                "CK_HouseholdInvitations_ExpiresAfterCreation",
                "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint(
                "CK_HouseholdInvitations_TokenHashLength",
                "octet_length(\"TokenHash\") = 32");
            table.HasCheckConstraint(
                "CK_HouseholdInvitations_NormalizedEmail",
                "\"IntendedEmailNormalized\" = lower(btrim(\"IntendedEmailNormalized\"))");
            table.HasCheckConstraint(
                "CK_HouseholdInvitations_TerminalState",
                "(\"Status\" = 'Pending' AND \"AcceptedAt\" IS NULL AND \"AcceptedByUserAccountId\" IS NULL AND \"RevokedAt\" IS NULL AND \"RevokedByUserAccountId\" IS NULL) OR "
                + "(\"Status\" = 'Expired' AND \"AcceptedAt\" IS NULL AND \"AcceptedByUserAccountId\" IS NULL AND \"RevokedAt\" IS NULL AND \"RevokedByUserAccountId\" IS NULL) OR "
                + "(\"Status\" = 'Accepted' AND \"AcceptedAt\" IS NOT NULL AND \"AcceptedByUserAccountId\" IS NOT NULL AND \"RevokedAt\" IS NULL AND \"RevokedByUserAccountId\" IS NULL) OR "
                + "(\"Status\" = 'Revoked' AND \"AcceptedAt\" IS NULL AND \"AcceptedByUserAccountId\" IS NULL AND \"RevokedAt\" IS NOT NULL AND \"RevokedByUserAccountId\" IS NOT NULL)");
        });
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.IntendedEmailNormalized)
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(invitation => invitation.TokenHash)
            .HasColumnType("bytea")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(invitation => invitation.Status)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(invitation => invitation.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.HouseholdId, invitation.CreatedAt });
        builder.HasIndex(invitation => new
        {
            invitation.HouseholdId,
            invitation.IntendedEmailNormalized,
            invitation.Status,
        });
        builder.HasIndex(invitation => new
        {
            invitation.HouseholdId,
            invitation.IntendedEmailNormalized,
        })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
        builder.HasOne(invitation => invitation.Household)
            .WithMany(household => household.Invitations)
            .HasForeignKey(invitation => invitation.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(invitation => invitation.CreatedByUserAccount)
            .WithMany(account => account.CreatedHouseholdInvitations)
            .HasForeignKey(invitation => invitation.CreatedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(invitation => invitation.AcceptedByUserAccount)
            .WithMany(account => account.AcceptedHouseholdInvitations)
            .HasForeignKey(invitation => invitation.AcceptedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(invitation => invitation.RevokedByUserAccount)
            .WithMany(account => account.RevokedHouseholdInvitations)
            .HasForeignKey(invitation => invitation.RevokedByUserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
