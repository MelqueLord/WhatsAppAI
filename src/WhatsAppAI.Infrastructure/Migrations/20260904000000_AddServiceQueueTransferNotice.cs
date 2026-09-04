using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddServiceQueueTransferNotice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "transfer_notice",
            schema: "whatsappai",
            table: "service_queues",
            type: "character varying(160)",
            maxLength: 160,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "transfer_notice",
            schema: "whatsappai",
            table: "service_queues");
    }
}
