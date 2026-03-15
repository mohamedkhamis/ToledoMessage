using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Models;

namespace ToledoVault.Data.Configurations;

public class GlobalSettingConfiguration : IEntityTypeConfiguration<GlobalSetting>
{
    public void Configure(EntityTypeBuilder<GlobalSetting> builder)
    {
        builder.HasKey(static e => e.Id);
        builder.Property(static e => e.Id).ValueGeneratedNever();
        builder.Property(static e => e.Key).IsRequired().HasMaxLength(128);
        builder.HasIndex(static e => e.Key).IsUnique();
        builder.HasIndex(static e => e.Category);
        builder.Property(static e => e.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(static e => e.Description).HasMaxLength(500);
        builder.Property(static e => e.Category).IsRequired().HasMaxLength(64);
        builder.Property(static e => e.ValueType).IsRequired().HasMaxLength(20);
        builder.Property(static e => e.CurrentValue).IsRequired().HasMaxLength(1000);
        builder.Property(static e => e.DefaultValue).IsRequired().HasMaxLength(1000);
        builder.Property(static e => e.ValidationRules).HasMaxLength(1000);
        builder.Property(static e => e.SortOrder).HasDefaultValue(0);
        builder.Property(static e => e.LastModifiedAt).IsRequired();

        var now = DateTimeOffset.UtcNow;

        builder.HasData(
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "security.encryptionKeyLength",
                DisplayName = "Encryption Key Length",
                Description = "AES key size in bits for message encryption",
                Category = "Security",
                ValueType = "selection",
                CurrentValue = "256",
                DefaultValue = "256",
                ValidationRules = "{\"options\": [\"128\", \"192\", \"256\"]}",
                SortOrder = 0,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "security.pbkdf2Iterations",
                DisplayName = "PBKDF2 Iterations",
                Description = "Number of iterations for password-based key derivation",
                Category = "Security",
                ValueType = "integer",
                CurrentValue = "600000",
                DefaultValue = "600000",
                ValidationRules = "{\"min\": 100000, \"max\": 1000000}",
                SortOrder = 1,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "appearance.defaultTheme",
                DisplayName = "Default Theme",
                Description = "Default theme for new users",
                Category = "Appearance",
                ValueType = "selection",
                CurrentValue = "default",
                DefaultValue = "default",
                ValidationRules = "{\"options\": [\"default\", \"default-dark\", \"whatsapp\", \"whatsapp-dark\", \"telegram\", \"telegram-dark\", \"signal\", \"signal-dark\"]}",
                SortOrder = 0,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "appearance.defaultFontSize",
                DisplayName = "Default Font Size",
                Description = "Default font size for new users",
                Category = "Appearance",
                ValueType = "selection",
                CurrentValue = "medium",
                DefaultValue = "medium",
                ValidationRules = "{\"options\": [\"small\", \"medium\", \"large\"]}",
                SortOrder = 1,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "features.readReceipts",
                DisplayName = "Read Receipts",
                Description = "Allow users to see when messages are read",
                Category = "Features",
                ValueType = "boolean",
                CurrentValue = "true",
                DefaultValue = "true",
                ValidationRules = null,
                SortOrder = 0,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "features.typingIndicators",
                DisplayName = "Typing Indicators",
                Description = "Show when users are typing",
                Category = "Features",
                ValueType = "boolean",
                CurrentValue = "true",
                DefaultValue = "true",
                ValidationRules = null,
                SortOrder = 1,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "features.linkPreviews",
                DisplayName = "Link Previews",
                Description = "Generate preview cards for shared URLs",
                Category = "Features",
                ValueType = "boolean",
                CurrentValue = "true",
                DefaultValue = "true",
                ValidationRules = null,
                SortOrder = 2,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "features.voiceMessages",
                DisplayName = "Voice Messages",
                Description = "Allow users to send voice messages",
                Category = "Features",
                ValueType = "boolean",
                CurrentValue = "true",
                DefaultValue = "true",
                ValidationRules = null,
                SortOrder = 3,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "logging.minLevel",
                DisplayName = "Minimum Log Level",
                Description = "Minimum severity level for log entries",
                Category = "Logging",
                ValueType = "selection",
                CurrentValue = "Information",
                DefaultValue = "Information",
                ValidationRules = "{\"options\": [\"Verbose\", \"Debug\", \"Information\", \"Warning\", \"Error\", \"Fatal\"]}",
                SortOrder = 0,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "logging.retentionDays",
                DisplayName = "Log Retention Days",
                Description = "Number of days to retain log entries",
                Category = "Logging",
                ValueType = "integer",
                CurrentValue = "30",
                DefaultValue = "30",
                ValidationRules = "{\"min\": 1, \"max\": 365}",
                SortOrder = 1,
                LastModifiedAt = now
            },
            new GlobalSetting
            {
                Id = IdGenerator.GetNewId(),
                Key = "notifications.maxRetries",
                DisplayName = "Max Push Notification Retries",
                Description = "Number of times to retry failed push notifications",
                Category = "Notifications",
                ValueType = "integer",
                CurrentValue = "3",
                DefaultValue = "3",
                ValidationRules = "{\"min\": 1, \"max\": 10}",
                SortOrder = 0,
                LastModifiedAt = now
            }
        );
    }
}
