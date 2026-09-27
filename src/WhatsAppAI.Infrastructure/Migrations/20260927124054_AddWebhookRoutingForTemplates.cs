using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookRoutingForTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "phone_number_id",
                schema: "whatsappai",
                table: "webhook_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "event_kind",
                schema: "whatsappai",
                table: "webhook_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "routing_id",
                schema: "whatsappai",
                table: "webhook_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "routing_kind",
                schema: "whatsappai",
                table: "webhook_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PhoneNumber");

            migrationBuilder.Sql("""
                UPDATE whatsappai.webhook_events
                SET routing_id = phone_number_id,
                    event_kind = 'messages'
                WHERE routing_id = '' AND phone_number_id IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_routing_kind_routing_id_status",
                schema: "whatsappai",
                table: "webhook_events",
                columns: new[] { "routing_kind", "routing_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_events_routing_kind_routing_id_status",
                schema: "whatsappai",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "event_kind",
                schema: "whatsappai",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "routing_id",
                schema: "whatsappai",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "routing_kind",
                schema: "whatsappai",
                table: "webhook_events");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number_id",
                schema: "whatsappai",
                table: "webhook_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
