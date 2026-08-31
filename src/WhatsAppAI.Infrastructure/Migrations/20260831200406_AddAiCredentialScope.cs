using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCredentialScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "credential_scope",
                schema: "whatsappai",
                table: "ai_provider_credentials",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "TenantProject");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credential_scope",
                schema: "whatsappai",
                table: "ai_provider_credentials");
        }
    }
}
