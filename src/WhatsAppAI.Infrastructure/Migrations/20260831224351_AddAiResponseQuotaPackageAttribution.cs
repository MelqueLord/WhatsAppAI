using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiResponseQuotaPackageAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_response_quota_package_reference",
                schema: "whatsappai",
                table: "usage_ledger",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ai_response_quota_package_type",
                schema: "whatsappai",
                table: "usage_ledger",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "package_reference",
                schema: "whatsappai",
                table: "ai_response_quota_reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "base:legacy");

            migrationBuilder.AddColumn<string>(
                name: "package_type",
                schema: "whatsappai",
                table: "ai_response_quota_reservations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "BasePackage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_response_quota_package_reference",
                schema: "whatsappai",
                table: "usage_ledger");

            migrationBuilder.DropColumn(
                name: "ai_response_quota_package_type",
                schema: "whatsappai",
                table: "usage_ledger");

            migrationBuilder.DropColumn(
                name: "package_reference",
                schema: "whatsappai",
                table: "ai_response_quota_reservations");

            migrationBuilder.DropColumn(
                name: "package_type",
                schema: "whatsappai",
                table: "ai_response_quota_reservations");
        }
    }
}
