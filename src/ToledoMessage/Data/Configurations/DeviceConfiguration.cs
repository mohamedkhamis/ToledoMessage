using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(d => d.UserId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.HasIndex(d => d.UserId);
        builder.Property(d => d.DeviceName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.IdentityPublicKeyClassical).IsRequired();
        builder.Property(d => d.IdentityPublicKeyPostQuantum).IsRequired();
        builder.Property(d => d.SignedPreKeyPublic).IsRequired();
        builder.Property(d => d.SignedPreKeySignature).IsRequired();
        builder.Property(d => d.KyberPreKeyPublic).IsRequired();
        builder.Property(d => d.KyberPreKeySignature).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.LastSeenAt).IsRequired();
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
