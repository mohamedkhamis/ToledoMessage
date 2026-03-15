using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ToledoVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminCredentials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ValidationRules = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalizationOverrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ResourceKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsNewKey = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationOverrides", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_AdminCredentials_Username",
                table: "AdminCredentials",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalSettings_Category",
                table: "GlobalSettings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalSettings_Key",
                table: "GlobalSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalizationOverrides_ResourceKey_LanguageCode",
                table: "LocalizationOverrides",
                columns: new[] { "ResourceKey", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminCredentials");

            migrationBuilder.DropTable(
                name: "GlobalSettings");

            migrationBuilder.DropTable(
                name: "LocalizationOverrides");
        }
    }
}
