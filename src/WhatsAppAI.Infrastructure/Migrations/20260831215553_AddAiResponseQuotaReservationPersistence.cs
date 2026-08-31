using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiResponseQuotaReservationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_response_quota_reservations",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    committed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    release_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_response_quota_reservations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_response_top_up_requests",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_response_top_up_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_response_quota_reservations_tenant_id_idempotency_key",
                schema: "whatsappai",
                table: "ai_response_quota_reservations",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_response_quota_reservations_tenant_id_period_start_utc_s~",
                schema: "whatsappai",
                table: "ai_response_quota_reservations",
                columns: new[] { "tenant_id", "period_start_utc", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_response_quota_reservations_tenant_id_source_message_id",
                schema: "whatsappai",
                table: "ai_response_quota_reservations",
                columns: new[] { "tenant_id", "source_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_response_top_up_requests_tenant_id_idempotency_key",
                schema: "whatsappai",
                table: "ai_response_top_up_requests",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_response_top_up_requests_tenant_id_period_start_utc_stat~",
                schema: "whatsappai",
                table: "ai_response_top_up_requests",
                columns: new[] { "tenant_id", "period_start_utc", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_response_quota_reservations",
                schema: "whatsappai");

            migrationBuilder.DropTable(
                name: "ai_response_top_up_requests",
                schema: "whatsappai");
        }
    }
}
