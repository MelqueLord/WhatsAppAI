using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppTemplateMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "template_language",
                schema: "whatsappai",
                table: "messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_name",
                schema: "whatsappai",
                table: "messages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_parameters_json",
                schema: "whatsappai",
                table: "messages",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "template_language",
                schema: "whatsappai",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "template_name",
                schema: "whatsappai",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "template_parameters_json",
                schema: "whatsappai",
                table: "messages");
        }
    }
}
