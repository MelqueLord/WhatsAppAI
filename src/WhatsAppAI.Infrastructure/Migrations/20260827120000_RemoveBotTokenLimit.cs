using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class RemoveBotTokenLimit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "max_tokens_per_response",
            schema: "whatsappai",
            table: "bot_configurations");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(
            name: "max_tokens_per_response",
            schema: "whatsappai",
            table: "bot_configurations",
            type: "integer",
            nullable: false,
            defaultValue: 500);
}
