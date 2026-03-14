using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToledoVault.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFontSizeDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FontSize",
                table: "UserPreferences",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "15",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "medium");

            migrationBuilder.CreateIndex(
                name: "IX_User_DisplayName",
                table: "Users",
                column: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_DisplayName",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "FontSize",
                table: "UserPreferences",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "medium",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "15");
        }
    }
}
