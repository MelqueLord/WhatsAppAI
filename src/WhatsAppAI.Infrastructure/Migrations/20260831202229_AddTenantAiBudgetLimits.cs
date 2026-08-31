using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAiBudgetLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "monthly_ai_cost_limit_minor_units",
                schema: "whatsappai",
                table: "tenants",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "monthly_ai_token_limit",
                schema: "whatsappai",
                table: "tenants",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "monthly_ai_cost_limit_minor_units",
                schema: "whatsappai",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "monthly_ai_token_limit",
                schema: "whatsappai",
                table: "tenants");
        }
    }
}
