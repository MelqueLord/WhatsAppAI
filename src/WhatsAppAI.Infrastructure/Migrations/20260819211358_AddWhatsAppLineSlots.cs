using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppLineSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "connection_type",
                table: "whatsapp_accounts",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "OfficialApi");

            migrationBuilder.AddColumn<int>(
                name: "line_number",
                table: "whatsapp_accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_accounts_tenant_id_connection_type_line_number",
                table: "whatsapp_accounts",
                columns: new[] { "tenant_id", "connection_type", "line_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_whatsapp_accounts_tenant_id_connection_type_line_number",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "connection_type",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "line_number",
                table: "whatsapp_accounts");
        }
    }
}
