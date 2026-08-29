using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiModelPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "price_version",
                schema: "whatsappai",
                table: "usage_ledger",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_model_pricing",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_cost_per_1k_minor_units = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    output_cost_per_1k_minor_units = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_model_pricing", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_pricing_provider_model_id_effective_from_effective~",
                schema: "whatsappai",
                table: "ai_model_pricing",
                columns: new[] { "provider", "model_id", "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_model_pricing_provider_model_id_version",
                schema: "whatsappai",
                table: "ai_model_pricing",
                columns: new[] { "provider", "model_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_model_pricing",
                schema: "whatsappai");

            migrationBuilder.DropColumn(
                name: "price_version",
                schema: "whatsappai",
                table: "usage_ledger");
        }
    }
}
