using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(static d => d.Id);
        builder.Property(static d => d.Id).ValueGeneratedNever();
        builder.HasIndex(static d => d.UserId);
        builder.Property(static d => d.DeviceName).HasMaxLength(100).IsRequired();
        builder.Property(static d => d.IdentityPublicKeyClassical).IsRequired();
        builder.Property(static d => d.IdentityPublicKeyPostQuantum).IsRequired();
        builder.Property(static d => d.SignedPreKeyPublic).IsRequired();
        builder.Property(static d => d.SignedPreKeySignature).IsRequired();
        builder.Property(static d => d.KyberPreKeyPublic).IsRequired();
        builder.Property(static d => d.KyberPreKeySignature).IsRequired();
        builder.Property(static d => d.CreatedAt).IsRequired();
        builder.Property(static d => d.LastSeenAt).IsRequired();
        builder.Property(static d => d.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(static d => d.User)
            .WithMany(static u => u.Devices)
            .HasForeignKey(static d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
