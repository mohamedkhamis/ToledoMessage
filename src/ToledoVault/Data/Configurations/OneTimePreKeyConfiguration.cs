using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class OneTimePreKeyConfiguration : IEntityTypeConfiguration<OneTimePreKey>
{
    public void Configure(EntityTypeBuilder<OneTimePreKey> builder)
    {
        builder.HasKey(static k => k.Id);
        builder.Property(static k => k.Id).ValueGeneratedNever();
        builder.HasIndex(static k => new { k.DeviceId, k.KeyId }).IsUnique();
        builder.Property(static k => k.PublicKey).IsRequired();
        builder.Property(static k => k.IsUsed).IsRequired().HasDefaultValue(false);

        builder.HasOne(static k => k.Device)
            .WithMany(static d => d.OneTimePreKeys)
            .HasForeignKey(static k => k.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
