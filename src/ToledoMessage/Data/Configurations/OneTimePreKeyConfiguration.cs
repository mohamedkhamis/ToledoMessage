using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class OneTimePreKeyConfiguration : IEntityTypeConfiguration<OneTimePreKey>
{
    public void Configure(EntityTypeBuilder<OneTimePreKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(k => k.DeviceId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.HasIndex(k => new { k.DeviceId, k.KeyId }).IsUnique();
        builder.Property(k => k.PublicKey).IsRequired();
        builder.Property(k => k.IsUsed).IsRequired().HasDefaultValue(false);

        builder.HasOne(k => k.Device)
            .WithMany(d => d.OneTimePreKeys)
            .HasForeignKey(k => k.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
