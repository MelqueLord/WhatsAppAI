using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceLinesAndAiQueueRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "routing_queue_ids_json",
                table: "ai_provider_credentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "routing_tag_ids_json",
                table: "ai_provider_credentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "queue_id",
                table: "conversations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_queues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_service_queues", item => item.id));

            migrationBuilder.CreateIndex(
                name: "IX_service_queues_tenant_id_name",
                table: "service_queues",
                columns: new[] { "tenant_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "service_queues");

            migrationBuilder.DropColumn(
                name: "routing_queue_ids_json",
                table: "ai_provider_credentials");

            migrationBuilder.DropColumn(
                name: "routing_tag_ids_json",
                table: "ai_provider_credentials");

            migrationBuilder.DropColumn(
                name: "queue_id",
                table: "conversations");
        }
    }
}
