using FamilyDashboard.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyDashboard.Api.Persistence.Configurations;

public sealed class UserAccountEntityConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(account => account.PrimaryEmail).HasMaxLength(320).IsRequired();
        builder.Property(account => account.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(account => account.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(account => account.PrimaryEmail);
    }
}
