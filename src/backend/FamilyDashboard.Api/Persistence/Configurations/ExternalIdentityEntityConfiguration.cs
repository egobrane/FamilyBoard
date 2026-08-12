using FamilyDashboard.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class ExternalIdentityEntityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("ExternalIdentities");
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Provider).HasMaxLength(32).IsRequired();
        builder.Property(identity => identity.ProviderSubject).HasMaxLength(255).IsRequired();
        builder.Property(identity => identity.Email).HasMaxLength(320);
        builder.Property(identity => identity.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(identity => new { identity.Provider, identity.ProviderSubject }).IsUnique();
        builder.HasIndex(identity => identity.UserAccountId);
        builder.HasOne(identity => identity.UserAccount)
            .WithMany(account => account.ExternalIdentities)
            .HasForeignKey(identity => identity.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
