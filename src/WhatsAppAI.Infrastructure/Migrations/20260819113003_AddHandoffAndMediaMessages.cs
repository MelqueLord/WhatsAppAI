using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoffAndMediaMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandoffMessage",
                table: "bot_configurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaMessage",
                table: "bot_configurations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandoffMessage",
                table: "bot_configurations");

            migrationBuilder.DropColumn(
                name: "MediaMessage",
                table: "bot_configurations");
        }
    }
}
