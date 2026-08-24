using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueTransferMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MediaMessage",
                table: "bot_configurations",
                newName: "media_message");

            migrationBuilder.RenameColumn(
                name: "HandoffMessage",
                table: "bot_configurations",
                newName: "handoff_message");

            migrationBuilder.AddColumn<string>(
                name: "queue_transfer_message",
                table: "bot_configurations",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "queue_transfer_message",
                table: "bot_configurations");

            migrationBuilder.RenameColumn(
                name: "media_message",
                table: "bot_configurations",
                newName: "MediaMessage");

            migrationBuilder.RenameColumn(
                name: "handoff_message",
                table: "bot_configurations",
                newName: "HandoffMessage");
        }
    }
}
