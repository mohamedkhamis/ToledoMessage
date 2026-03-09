using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToledoMessage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayNameSecondary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayNameSecondary",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayNameSecondary",
                table: "Users");
        }
    }
}
