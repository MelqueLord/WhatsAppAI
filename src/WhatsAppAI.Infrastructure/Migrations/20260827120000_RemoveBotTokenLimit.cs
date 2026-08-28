using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827120000_RemoveBotTokenLimit")]
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
