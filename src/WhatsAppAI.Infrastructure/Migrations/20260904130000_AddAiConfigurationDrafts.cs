using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddAiConfigurationDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "draft_system_prompt", table: "ai_provider_credentials", type: "character varying(4000)", maxLength: 4000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "draft_routing_queue_ids_json", table: "ai_provider_credentials", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "draft_routing_tag_ids_json", table: "ai_provider_credentials", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>(name: "draft_max_tokens_per_response", table: "ai_provider_credentials", type: "integer", nullable: false, defaultValue: 180);
        migrationBuilder.AddColumn<double>(name: "draft_confidence_threshold", table: "ai_provider_credentials", type: "double precision", nullable: false, defaultValue: 0.5);
        migrationBuilder.AddColumn<long>(name: "draft_version", table: "ai_provider_credentials", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<long>(name: "published_version", table: "ai_provider_credentials", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<DateTime>(name: "published_at", table: "ai_provider_credentials", type: "timestamp with time zone", nullable: true);

        migrationBuilder.Sql("UPDATE ai_provider_credentials SET draft_system_prompt = system_prompt, draft_routing_queue_ids_json = routing_queue_ids_json, draft_routing_tag_ids_json = routing_tag_ids_json, draft_max_tokens_per_response = max_tokens_per_response, draft_version = version, published_version = version, published_at = COALESCE(updated_at, created_at) WHERE draft_version = 0 AND version > 0;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "draft_system_prompt", table: "ai_provider_credentials");
        migrationBuilder.DropColumn(name: "draft_routing_queue_ids_json", table: "ai_provider_credentials");
        migrationBuilder.DropColumn(name: "draft_routing_tag_ids_json", table: "ai_provider_credentials");
        migrationBuilder.DropColumn(name: "draft_max_tokens_per_response", table: "ai_provider_credentials");
        migrationBuilder.DropColumn(name: "draft_confidence_threshold", table: "ai_provider_credentials");
        migrationBuilder.DropColumn(name: "draft_version", table: "ai_provider_credentials");
        migrationBuilder.DropColumn(name: "published_version", table: "ai_provider_credentials");
        migrationBuilder.DropColumn(name: "published_at", table: "ai_provider_credentials");
    }
}
