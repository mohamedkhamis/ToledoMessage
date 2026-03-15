using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class LocalizationOverrideConfiguration : IEntityTypeConfiguration<LocalizationOverride>
{
    public void Configure(EntityTypeBuilder<LocalizationOverride> builder)
    {
        builder.HasKey(static e => e.Id);
        builder.Property(static e => e.Id).ValueGeneratedNever();
        builder.Property(static e => e.ResourceKey).IsRequired().HasMaxLength(256);
        builder.Property(static e => e.LanguageCode).IsRequired().HasMaxLength(10);
        builder.HasIndex(static e => new { e.ResourceKey, e.LanguageCode }).IsUnique();
        builder.Property(static e => e.Value).IsRequired().HasMaxLength(4000);
        builder.Property(static e => e.IsNewKey).HasDefaultValue(false);
        builder.Property(static e => e.LastModifiedAt).IsRequired();
    }
}
