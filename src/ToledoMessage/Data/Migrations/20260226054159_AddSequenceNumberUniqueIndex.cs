using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToledoMessage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EncryptedMessages_ConversationId",
                table: "EncryptedMessages");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedMessages_ConversationId_SequenceNumber",
                table: "EncryptedMessages",
                columns: new[] { "ConversationId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EncryptedMessages_ConversationId_SequenceNumber",
                table: "EncryptedMessages");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedMessages_ConversationId",
                table: "EncryptedMessages",
                column: "ConversationId");
        }
    }
}
