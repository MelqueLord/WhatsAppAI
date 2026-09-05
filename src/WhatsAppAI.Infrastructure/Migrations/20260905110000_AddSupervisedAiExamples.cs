using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[Migration("20260905110000_AddSupervisedAiExamples")]
public partial class AddSupervisedAiExamples : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "source",
            schema: "whatsappai",
            table: "ai_response_examples",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Manual");

        migrationBuilder.AddColumn<Guid>(
            name: "source_interaction_id",
            schema: "whatsappai",
            table: "ai_response_examples",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_ai_response_examples_tenant_id_source_interaction_id",
            schema: "whatsappai",
            table: "ai_response_examples",
            columns: new[] { "tenant_id", "source_interaction_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_ai_response_examples_tenant_id_source_interaction_id",
            schema: "whatsappai",
            table: "ai_response_examples");

        migrationBuilder.DropColumn(name: "source", schema: "whatsappai", table: "ai_response_examples");
        migrationBuilder.DropColumn(name: "source_interaction_id", schema: "whatsappai", table: "ai_response_examples");
    }
}
