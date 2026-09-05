using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddAiResponseFeedback : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "response_message_id",
            schema: "whatsappai",
            table: "ai_interactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "feedback_rating",
            schema: "whatsappai",
            table: "ai_interactions",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "feedback_note",
            schema: "whatsappai",
            table: "ai_interactions",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "corrected_response",
            schema: "whatsappai",
            table: "ai_interactions",
            type: "character varying(160)",
            maxLength: 160,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "feedback_by_user_id",
            schema: "whatsappai",
            table: "ai_interactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "feedback_at",
            schema: "whatsappai",
            table: "ai_interactions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_ai_interactions_tenant_id_response_message_id",
            schema: "whatsappai",
            table: "ai_interactions",
            columns: new[] { "tenant_id", "response_message_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_ai_interactions_tenant_id_response_message_id",
            schema: "whatsappai",
            table: "ai_interactions");

        migrationBuilder.DropColumn(name: "response_message_id", schema: "whatsappai", table: "ai_interactions");
        migrationBuilder.DropColumn(name: "feedback_rating", schema: "whatsappai", table: "ai_interactions");
        migrationBuilder.DropColumn(name: "feedback_note", schema: "whatsappai", table: "ai_interactions");
        migrationBuilder.DropColumn(name: "corrected_response", schema: "whatsappai", table: "ai_interactions");
        migrationBuilder.DropColumn(name: "feedback_by_user_id", schema: "whatsappai", table: "ai_interactions");
        migrationBuilder.DropColumn(name: "feedback_at", schema: "whatsappai", table: "ai_interactions");
    }
}
