using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(static u => u.Id);
        builder.Property(static u => u.Id).ValueGeneratedNever();
        builder.Property(static u => u.Username).HasMaxLength(32).IsRequired();
        builder.HasIndex(static u => u.Username).IsUnique();
        builder.Property(static u => u.DisplayName).HasMaxLength(50).IsRequired();
        // FR-015: Index for efficient user search queries
        builder.HasIndex(static u => u.DisplayName).HasDatabaseName("IX_User_DisplayName");
        builder.Property(static u => u.DisplayNameSecondary).HasMaxLength(50);
        builder.Property(static u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(static u => u.CreatedAt).IsRequired();
        builder.Property(static u => u.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(static u => u.DeletionRequestedAt);
    }
}
