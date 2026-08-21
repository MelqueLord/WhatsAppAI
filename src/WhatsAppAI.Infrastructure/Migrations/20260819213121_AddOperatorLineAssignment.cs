using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    public partial class AddOperatorLineAssignment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assigned_connection_type",
                table: "tenant_memberships",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "assigned_line_number",
                table: "tenant_memberships",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "assigned_connection_type", table: "tenant_memberships");
            migrationBuilder.DropColumn(name: "assigned_line_number", table: "tenant_memberships");
        }
    }
}