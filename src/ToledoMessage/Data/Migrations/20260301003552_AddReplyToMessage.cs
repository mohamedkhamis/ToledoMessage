using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToledoMessage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReplyToMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReplyToMessageId",
                table: "EncryptedMessages",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "EncryptedMessages");
        }
    }
}
