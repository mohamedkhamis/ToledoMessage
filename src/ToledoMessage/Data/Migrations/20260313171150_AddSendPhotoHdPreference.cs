using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToledoMessage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSendPhotoHdPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SendPhotoHd",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SendPhotoHd",
                table: "UserPreferences");
        }
    }
}
