using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class AdminCredentialConfiguration : IEntityTypeConfiguration<AdminCredential>
{
    public void Configure(EntityTypeBuilder<AdminCredential> builder)
    {
        builder.HasKey(static e => e.Id);
        builder.Property(static e => e.Id).ValueGeneratedNever();
        builder.Property(static e => e.Username).IsRequired().HasMaxLength(32);
        builder.HasIndex(static e => e.Username).IsUnique();
        builder.Property(static e => e.PasswordHash).IsRequired();
        builder.Property(static e => e.MustChangePassword).HasDefaultValue(true);
        builder.Property(static e => e.CreatedAt).IsRequired();
    }
}
