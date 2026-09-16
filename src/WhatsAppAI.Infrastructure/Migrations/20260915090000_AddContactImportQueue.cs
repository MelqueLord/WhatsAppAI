using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddContactImportQueue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "queue_id",
            schema: "whatsappai",
            table: "contacts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_contacts_queue_id",
            schema: "whatsappai",
            table: "contacts",
            column: "queue_id");

        migrationBuilder.CreateIndex(
            name: "ix_contacts_tenant_id_queue_id",
            schema: "whatsappai",
            table: "contacts",
            columns: new[] { "tenant_id", "queue_id" });

        migrationBuilder.AddForeignKey(
            name: "fk_contacts_service_queues_queue_id",
            schema: "whatsappai",
            table: "contacts",
            column: "queue_id",
            principalSchema: "whatsappai",
            principalTable: "service_queues",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_contacts_service_queues_queue_id",
            schema: "whatsappai",
            table: "contacts");

        migrationBuilder.DropIndex(
            name: "ix_contacts_queue_id",
            schema: "whatsappai",
            table: "contacts");

        migrationBuilder.DropIndex(
            name: "ix_contacts_tenant_id_queue_id",
            schema: "whatsappai",
            table: "contacts");

        migrationBuilder.DropColumn(
            name: "queue_id",
            schema: "whatsappai",
            table: "contacts");
    }
}
