using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260904000000_AddServiceQueueTransferNotice")]
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
