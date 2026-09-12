using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddServiceQueueInteractionReply : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "interaction_reply",
            schema: "whatsappai",
            table: "service_queues",
            type: "character varying(160)",
            maxLength: 160,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "interaction_reply",
            schema: "whatsappai",
            table: "service_queues");
    }
}
