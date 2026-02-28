using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoMessage.Models;

namespace ToledoMessage.Data.Configurations;

public class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.HasKey(static p => p.Id);
        builder.Property(static p => p.Id).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.Property(static p => p.UserId).HasColumnType("decimal(28,8)").HasPrecision(28, 8);
        builder.HasIndex(static p => p.UserId).IsUnique();
        builder.HasOne(static p => p.User).WithOne().HasForeignKey<UserPreferences>(static p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(static p => p.Theme).HasMaxLength(50).HasDefaultValue("default");
        builder.Property(static p => p.FontSize).HasMaxLength(20).HasDefaultValue("medium");
        builder.Property(static p => p.Language).HasMaxLength(10).HasDefaultValue("en");
        builder.Property(static p => p.NotificationsEnabled).HasDefaultValue(true);
        builder.Property(static p => p.ReadReceiptsEnabled).HasDefaultValue(true);
        builder.Property(static p => p.TypingIndicatorsEnabled).HasDefaultValue(true);
        builder.Property(static p => p.CreatedAt).IsRequired();
        builder.Property(static p => p.UpdatedAt).IsRequired();
    }
}
