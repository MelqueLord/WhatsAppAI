using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxMessageDeletionMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_for_everyone_at",
                schema: "whatsappai",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "hidden_from_inbox_at",
                schema: "whatsappai",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_for_everyone_at",
                schema: "whatsappai",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "hidden_from_inbox_at",
                schema: "whatsappai",
                table: "messages");
        }
    }
}
