using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class EncryptedKeyBackupConfiguration : IEntityTypeConfiguration<EncryptedKeyBackup>
{
    public void Configure(EntityTypeBuilder<EncryptedKeyBackup> builder)
    {
        builder.HasKey(static b => b.Id);
        builder.Property(static b => b.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static b => b.UserId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.HasIndex(static b => b.UserId).IsUnique();
        builder.HasOne(static b => b.User).WithOne().HasForeignKey<EncryptedKeyBackup>(static b => b.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(static b => b.EncryptedBlob).IsRequired();
        builder.Property(static b => b.Salt).IsRequired().HasMaxLength(16);
        builder.Property(static b => b.Nonce).IsRequired().HasMaxLength(12);
        builder.Property(static b => b.Version).HasDefaultValue(1);
        builder.Property(static b => b.CreatedAt).IsRequired();
        builder.Property(static b => b.UpdatedAt).IsRequired();
    }
}
