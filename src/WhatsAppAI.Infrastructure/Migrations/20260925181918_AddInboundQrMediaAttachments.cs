using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundQrMediaAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbound_media_attachments",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    length = table.Column<long>(type: "bigint", nullable: false),
                    encrypted_content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_media_attachments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_media_attachments_tenant_id",
                schema: "whatsappai",
                table: "inbound_media_attachments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_media_attachments_tenant_id_external_message_id",
                schema: "whatsappai",
                table: "inbound_media_attachments",
                columns: new[] { "tenant_id", "external_message_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbound_media_attachments",
                schema: "whatsappai");
        }
    }
}
