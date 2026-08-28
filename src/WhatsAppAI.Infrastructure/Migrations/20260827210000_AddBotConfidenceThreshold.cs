using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827210000_AddBotConfidenceThreshold")]
public sealed class AddBotConfidenceThreshold : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "confidence_threshold",
            schema: "whatsappai",
            table: "bot_configurations",
            type: "double precision",
            nullable: false,
            defaultValue: 0.5);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "confidence_threshold",
            schema: "whatsappai",
            table: "bot_configurations");
    }
}
