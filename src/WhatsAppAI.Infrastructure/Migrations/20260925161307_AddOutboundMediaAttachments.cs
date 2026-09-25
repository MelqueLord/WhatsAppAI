using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundMediaAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbound_media_attachments",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    length = table.Column<long>(type: "bigint", nullable: false),
                    encrypted_content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    purged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbound_media_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_outbound_media_attachments_messages_message_id",
                        column: x => x.message_id,
                        principalSchema: "whatsappai",
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbound_media_attachments_message_id",
                schema: "whatsappai",
                table: "outbound_media_attachments",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbound_media_attachments_tenant_id",
                schema: "whatsappai",
                table: "outbound_media_attachments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_media_attachments_tenant_id_message_id",
                schema: "whatsappai",
                table: "outbound_media_attachments",
                columns: new[] { "tenant_id", "message_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbound_media_attachments",
                schema: "whatsappai");
        }
    }
}
