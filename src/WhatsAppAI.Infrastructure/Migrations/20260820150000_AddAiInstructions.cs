using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    public partial class AddAiInstructions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "system_prompt",
                table: "ai_provider_credentials",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_tokens_per_response",
                table: "ai_provider_credentials",
                type: "int",
                nullable: false,
                defaultValue: 500);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "system_prompt", table: "ai_provider_credentials");
            migrationBuilder.DropColumn(name: "max_tokens_per_response", table: "ai_provider_credentials");
        }
    }
}