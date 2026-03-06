using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToledoMessage.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsReadWithReadPointer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "EncryptedMessages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "EncryptedMessages");

            migrationBuilder.CreateTable(
                name: "ConversationReadPointers",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ConversationId = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    LastReadSequenceNumber = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    UnreadCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationReadPointers", x => new { x.UserId, x.ConversationId });
                    table.ForeignKey(
                        name: "FK_ConversationReadPointers_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationReadPointers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationReadPointers_ConversationId",
                table: "ConversationReadPointers",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationReadPointers");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "EncryptedMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                table: "EncryptedMessages",
                type: "datetimeoffset",
                nullable: true);
        }
    }
}
