using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ToledoVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 609358493797075579L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 624708109479566483L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 3312535075451403539L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 4202225848195657054L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 4234894333481566928L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 4267429388408400089L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 6159339716170130721L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 7062314752539193280L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 7985863922861211497L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 8498888103287051600L);

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "SortOrder", "ValidationRules", "ValueType" },
                values: new object[,]
                {
                    { 1647305997828296015L, "Appearance", "medium", "medium", "Default font size for new users", "Default Font Size", "appearance.defaultFontSize", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), 1, "{\"options\": [\"small\", \"medium\", \"large\"]}", "selection" },
                    { 2186619364910816402L, "Logging", "30", "30", "Number of days to retain log entries", "Log Retention Days", "logging.retentionDays", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), 1, "{\"min\": 1, \"max\": 365}", "integer" }
                });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "ValidationRules", "ValueType" },
                values: new object[,]
                {
                    { 2375481900587111243L, "Security", "256", "256", "AES key size in bits for message encryption", "Encryption Key Length", "security.encryptionKeyLength", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), "{\"options\": [\"128\", \"192\", \"256\"]}", "selection" },
                    { 3134461825223462409L, "Notifications", "3", "3", "Number of times to retry failed push notifications", "Max Push Notification Retries", "notifications.maxRetries", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), "{\"min\": 1, \"max\": 10}", "integer" },
                    { 3547443041045299879L, "Features", "true", "true", "Allow users to see when messages are read", "Read Receipts", "features.readReceipts", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), null, "boolean" },
                    { 4932607441112003257L, "Appearance", "default", "default", "Default theme for new users", "Default Theme", "appearance.defaultTheme", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), "{\"options\": [\"default\", \"default-dark\", \"whatsapp\", \"whatsapp-dark\", \"telegram\", \"telegram-dark\", \"signal\", \"signal-dark\"]}", "selection" },
                    { 5084024938015077971L, "Logging", "Information", "Information", "Minimum severity level for log entries", "Minimum Log Level", "logging.minLevel", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), "{\"options\": [\"Verbose\", \"Debug\", \"Information\", \"Warning\", \"Error\", \"Fatal\"]}", "selection" }
                });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "SortOrder", "ValidationRules", "ValueType" },
                values: new object[,]
                {
                    { 6261558502429981551L, "Features", "true", "true", "Allow users to send voice messages", "Voice Messages", "features.voiceMessages", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), 3, null, "boolean" },
                    { 6767577967081690152L, "Security", "600000", "600000", "Number of iterations for password-based key derivation", "PBKDF2 Iterations", "security.pbkdf2Iterations", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), 1, "{\"min\": 100000, \"max\": 1000000}", "integer" },
                    { 8186949666844831051L, "Features", "true", "true", "Show when users are typing", "Typing Indicators", "features.typingIndicators", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), 1, null, "boolean" },
                    { 8841052925682027482L, "Features", "true", "true", "Generate preview cards for shared URLs", "Link Previews", "features.linkPreviews", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 43, 1, 902, DateTimeKind.Unspecified).AddTicks(5875), new TimeSpan(0, 0, 0, 0, 0)), 2, null, "boolean" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 1647305997828296015L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 2186619364910816402L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 2375481900587111243L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 3134461825223462409L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 3547443041045299879L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 4932607441112003257L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 5084024938015077971L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 6261558502429981551L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 6767577967081690152L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 8186949666844831051L);

            migrationBuilder.DeleteData(
                table: "GlobalSettings",
                keyColumn: "Id",
                keyValue: 8841052925682027482L);

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "ValidationRules", "ValueType" },
                values: new object[] { 609358493797075579L, "Logging", "Information", "Information", "Minimum severity level for log entries", "Minimum Log Level", "logging.minLevel", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), "{\"options\": [\"Verbose\", \"Debug\", \"Information\", \"Warning\", \"Error\", \"Fatal\"]}", "selection" });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "SortOrder", "ValidationRules", "ValueType" },
                values: new object[] { 624708109479566483L, "Features", "true", "true", "Show when users are typing", "Typing Indicators", "features.typingIndicators", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), 1, null, "boolean" });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "ValidationRules", "ValueType" },
                values: new object[] { 3312535075451403539L, "Appearance", "default", "default", "Default theme for new users", "Default Theme", "appearance.defaultTheme", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), "{\"options\": [\"default\", \"default-dark\", \"whatsapp\", \"whatsapp-dark\", \"telegram\", \"telegram-dark\", \"signal\", \"signal-dark\"]}", "selection" });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "SortOrder", "ValidationRules", "ValueType" },
                values: new object[,]
                {
                    { 4202225848195657054L, "Appearance", "medium", "medium", "Default font size for new users", "Default Font Size", "appearance.defaultFontSize", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), 1, "{\"options\": [\"small\", \"medium\", \"large\"]}", "selection" },
                    { 4234894333481566928L, "Security", "600000", "600000", "Number of iterations for password-based key derivation", "PBKDF2 Iterations", "security.pbkdf2Iterations", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), 1, "{\"min\": 100000, \"max\": 1000000}", "integer" },
                    { 4267429388408400089L, "Features", "true", "true", "Generate preview cards for shared URLs", "Link Previews", "features.linkPreviews", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), 2, null, "boolean" },
                    { 6159339716170130721L, "Logging", "30", "30", "Number of days to retain log entries", "Log Retention Days", "logging.retentionDays", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), 1, "{\"min\": 1, \"max\": 365}", "integer" }
                });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "ValidationRules", "ValueType" },
                values: new object[] { 7062314752539193280L, "Features", "true", "true", "Allow users to see when messages are read", "Read Receipts", "features.readReceipts", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), null, "boolean" });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "SortOrder", "ValidationRules", "ValueType" },
                values: new object[] { 7985863922861211497L, "Features", "true", "true", "Allow users to send voice messages", "Voice Messages", "features.voiceMessages", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), 3, null, "boolean" });

            migrationBuilder.InsertData(
                table: "GlobalSettings",
                columns: new[] { "Id", "Category", "CurrentValue", "DefaultValue", "Description", "DisplayName", "Key", "LastModifiedAt", "ValidationRules", "ValueType" },
                values: new object[] { 8498888103287051600L, "Security", "256", "256", "AES key size in bits for message encryption", "Encryption Key Length", "security.encryptionKeyLength", new DateTimeOffset(new DateTime(2026, 3, 14, 20, 17, 2, 668, DateTimeKind.Unspecified).AddTicks(1994), new TimeSpan(0, 0, 0, 0, 0)), "{\"options\": [\"128\", \"192\", \"256\"]}", "selection" });
        }
    }
}
