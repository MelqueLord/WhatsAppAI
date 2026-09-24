using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddOfficialTemplateBroadcasts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "delivery_mode", schema: "whatsappai", table: "broadcast_lists", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "QrCodeText");
        migrationBuilder.AddColumn<string>(name: "template_name", schema: "whatsappai", table: "broadcast_lists", type: "character varying(512)", maxLength: 512, nullable: true);
        migrationBuilder.AddColumn<string>(name: "template_language", schema: "whatsappai", table: "broadcast_lists", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "template_parameters_json", schema: "whatsappai", table: "broadcast_lists", type: "character varying(12000)", maxLength: 12000, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "outbound_message_id", schema: "whatsappai", table: "broadcast_recipients", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<int>(name: "dispatch_attempt", schema: "whatsappai", table: "broadcast_recipients", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<DateTime>(name: "queued_at", schema: "whatsappai", table: "broadcast_recipients", type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_broadcast_recipients_outbound_message_id", schema: "whatsappai", table: "broadcast_recipients", column: "outbound_message_id", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_broadcast_recipients_outbound_message_id", schema: "whatsappai", table: "broadcast_recipients");
        migrationBuilder.DropColumn(name: "outbound_message_id", schema: "whatsappai", table: "broadcast_recipients");
        migrationBuilder.DropColumn(name: "dispatch_attempt", schema: "whatsappai", table: "broadcast_recipients");
        migrationBuilder.DropColumn(name: "queued_at", schema: "whatsappai", table: "broadcast_recipients");
        migrationBuilder.DropColumn(name: "delivery_mode", schema: "whatsappai", table: "broadcast_lists");
        migrationBuilder.DropColumn(name: "template_name", schema: "whatsappai", table: "broadcast_lists");
        migrationBuilder.DropColumn(name: "template_language", schema: "whatsappai", table: "broadcast_lists");
        migrationBuilder.DropColumn(name: "template_parameters_json", schema: "whatsappai", table: "broadcast_lists");
    }
}
