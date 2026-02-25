using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(u => u.DisplayName).HasMaxLength(50).IsRequired();
        builder.HasIndex(u => u.DisplayName).IsUnique();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
    }
}
