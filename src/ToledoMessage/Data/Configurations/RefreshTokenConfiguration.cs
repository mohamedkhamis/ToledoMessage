using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(rt => rt.UserId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(rt => rt.DeviceId).HasColumnType("decimal(28,8)").HasPrecision(28, 8).IsRequired(false);
        builder.Property(rt => rt.Token).HasMaxLength(512).IsRequired();
        builder.HasIndex(rt => rt.Token).IsUnique();
        builder.Property(rt => rt.ExpiresAt).IsRequired();
        builder.Property(rt => rt.CreatedAt).IsRequired();
        builder.Property(rt => rt.IsRevoked).IsRequired().HasDefaultValue(false);

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rt => rt.Device)
            .WithMany()
            .HasForeignKey(rt => rt.DeviceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
