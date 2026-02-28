using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(static rt => rt.Id);
        builder.Property(static rt => rt.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static rt => rt.UserId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static rt => rt.DeviceId).HasColumnType("decimal(28,8)").HasPrecision(28, 8).IsRequired(false);
        builder.Property(static rt => rt.Token).HasMaxLength(512).IsRequired();
        builder.HasIndex(static rt => rt.Token).IsUnique();
        builder.Property(static rt => rt.ExpiresAt).IsRequired();
        builder.Property(static rt => rt.CreatedAt).IsRequired();
        builder.Property(static rt => rt.IsRevoked).IsRequired().HasDefaultValue(false);

        builder.HasOne(static rt => rt.User)
            .WithMany(static u => u.RefreshTokens)
            .HasForeignKey(static rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(static rt => rt.Device)
            .WithMany()
            .HasForeignKey(static rt => rt.DeviceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
