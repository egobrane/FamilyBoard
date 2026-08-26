using FamilyDashboard.Api.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class GoogleTasksConnectionEntityConfiguration
    : IEntityTypeConfiguration<GoogleTasksConnection>
{
    public void Configure(EntityTypeBuilder<GoogleTasksConnection> builder)
    {
        builder.ToTable("GoogleTasksConnections", table => table.HasCheckConstraint(
            "CK_GoogleTasksConnections_Tokens",
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
            .WithOne(account => account.GoogleTasksConnection)
            .HasForeignKey<GoogleTasksConnection>(connection => connection.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
