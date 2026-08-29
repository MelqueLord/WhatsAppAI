using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePlanLineQuotaToTotal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "default_official_api_line_count",
                schema: "whatsappai",
                table: "subscription_plans",
                newName: "default_line_count");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "default_line_count",
                schema: "whatsappai",
                table: "subscription_plans",
                newName: "default_official_api_line_count");
        }
    }
}
