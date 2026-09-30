using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingBroadcastConsentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "skipped_count",
                schema: "whatsappai",
                table: "broadcast_lists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "template_category",
                schema: "whatsappai",
                table: "broadcast_lists",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "skipped_count",
                schema: "whatsappai",
                table: "broadcast_lists");

            migrationBuilder.DropColumn(
                name: "template_category",
                schema: "whatsappai",
                table: "broadcast_lists");
        }
    }
}
