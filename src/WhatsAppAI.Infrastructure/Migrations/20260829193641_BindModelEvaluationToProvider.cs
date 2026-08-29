using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BindModelEvaluationToProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider",
                schema: "whatsappai",
                table: "model_evaluations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "openai");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider",
                schema: "whatsappai",
                table: "model_evaluations");
        }
    }
}
