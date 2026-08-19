using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddTenantDueDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "due_date",
            table: "tenants",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE tenants SET due_date = DATE_ADD(created_at, INTERVAL 30 DAY) WHERE due_date IS NULL");

        migrationBuilder.AlterColumn<DateTime>(
            name: "due_date",
            table: "tenants",
            type: "datetime(6)",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "due_date",
            table: "tenants");
    }
}
