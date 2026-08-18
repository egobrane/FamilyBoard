using FamilyDashboard.Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class GoogleCalendarConnectionEntityConfiguration
    : IEntityTypeConfiguration<GoogleCalendarConnection>
{
    public void Configure(EntityTypeBuilder<GoogleCalendarConnection> builder)
    {
        builder.ToTable("GoogleCalendarConnections", table => table.HasCheckConstraint(
            "CK_GoogleCalendarConnections_Tokens",
            "(\"Status\" = 'Active' AND \"ProtectedRefreshToken\" IS NOT NULL) OR \"Status\" <> 'Active'"));
        builder.HasKey(connection => connection.Id);
        builder.HasAlternateKey(connection => new { connection.Id, connection.UserAccountId });
        builder.Property(connection => connection.ProviderSubject).HasMaxLength(255).IsRequired();
        builder.Property(connection => connection.ProviderEmailNormalized).HasMaxLength(320).IsRequired();
        builder.Property(connection => connection.ProtectedAccessToken).HasColumnType("text");
        builder.Property(connection => connection.ProtectedRefreshToken).HasColumnType("text");
        builder.Property(connection => connection.GrantedScopes).HasColumnType("text").IsRequired();
        builder.Property(connection => connection.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(connection => connection.UserAccountId).IsUnique();
        builder.HasIndex(connection => new { connection.ProviderSubject, connection.Status });
        builder.HasOne(connection => connection.UserAccount)
            .WithOne(account => account.GoogleCalendarConnection)
            .HasForeignKey<GoogleCalendarConnection>(connection => connection.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
