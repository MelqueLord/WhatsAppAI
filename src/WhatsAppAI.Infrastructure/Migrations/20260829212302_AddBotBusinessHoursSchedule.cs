using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBotBusinessHoursSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "business_hours_enabled",
                schema: "whatsappai",
                table: "bot_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "business_hours_json",
                schema: "whatsappai",
                table: "bot_configurations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                schema: "whatsappai",
                table: "bot_configurations",
                type: "character varying(100)",
                nullable: false,
                defaultValue: "America/Sao_Paulo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_hours_enabled",
                schema: "whatsappai",
                table: "bot_configurations");

            migrationBuilder.DropColumn(
                name: "business_hours_json",
                schema: "whatsappai",
                table: "bot_configurations");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                schema: "whatsappai",
                table: "bot_configurations");
        }
    }
}
