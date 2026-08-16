using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class EnforceIdentityBoundaries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM tenant_memberships WHERE user_id IN (SELECT id FROM users WHERE is_platform_admin = 1);");
        migrationBuilder.Sql("UPDATE tenant_memberships SET role = 'TenantOwner' WHERE role = 'Owner';");

        migrationBuilder.DropIndex(
            name: "ix_tenant_memberships_user_id",
            table: "tenant_memberships");

        migrationBuilder.CreateIndex(
            name: "ix_tenant_memberships_user_id",
            table: "tenant_memberships",
            column: "user_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_tenant_memberships_user_id",
            table: "tenant_memberships");

        migrationBuilder.CreateIndex(
            name: "ix_tenant_memberships_user_id",
            table: "tenant_memberships",
            column: "user_id");

        migrationBuilder.Sql("UPDATE tenant_memberships SET role = 'Owner' WHERE role = 'TenantOwner';");
    }
}
