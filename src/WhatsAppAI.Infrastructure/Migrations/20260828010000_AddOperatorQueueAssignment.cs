using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260828010000_AddOperatorQueueAssignment")]
public sealed class AddOperatorQueueAssignment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "assigned_queue_id",
            schema: "whatsappai",
            table: "tenant_memberships",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_tenant_memberships_assigned_queue_id",
            schema: "whatsappai",
            table: "tenant_memberships",
            column: "assigned_queue_id");

        migrationBuilder.AddForeignKey(
            name: "FK_tenant_memberships_service_queues_assigned_queue_id",
            schema: "whatsappai",
            table: "tenant_memberships",
            column: "assigned_queue_id",
            principalSchema: "whatsappai",
            principalTable: "service_queues",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_tenant_memberships_service_queues_assigned_queue_id",
            schema: "whatsappai",
            table: "tenant_memberships");

        migrationBuilder.DropIndex(
            name: "IX_tenant_memberships_assigned_queue_id",
            schema: "whatsappai",
            table: "tenant_memberships");

        migrationBuilder.DropColumn(
            name: "assigned_queue_id",
            schema: "whatsappai",
            table: "tenant_memberships");
    }
}
