using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppWebSessionLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "whatsapp_web_session_leases",
                schema: "whatsappai",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    owner_instance_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    owner_base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_web_session_leases", x => x.session_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_web_session_leases_expires_at",
                schema: "whatsappai",
                table: "whatsapp_web_session_leases",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_web_session_leases_tenant_id_line_number",
                schema: "whatsappai",
                table: "whatsapp_web_session_leases",
                columns: new[] { "tenant_id", "line_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_web_session_leases",
                schema: "whatsappai");
        }
    }
}
