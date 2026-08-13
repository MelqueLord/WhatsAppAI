using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialIdentitySchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                suspended_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                reactivated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                suspension_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                version = table.Column<uint>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenants", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                activated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_login_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tenant_memberships",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                deactivated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                reactivated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                version = table.Column<uint>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenant_memberships", x => x.id);
                table.ForeignKey(
                    name: "fk_tenant_memberships_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tenant_memberships_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "invitations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                consumed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                revoked_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                version = table.Column<uint>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_invitations", x => x.id);
                table.ForeignKey(
                    name: "fk_invitations_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_invitations_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_invitations_expires_at",
            table: "invitations",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_invitations_tenant_id_email_status",
            table: "invitations",
            columns: new[] { "tenant_id", "email", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_invitations_token_hash",
            table: "invitations",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_tenant_memberships_status",
            table: "tenant_memberships",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_tenant_memberships_tenant_id_user_id",
            table: "tenant_memberships",
            columns: new[] { "tenant_id", "user_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_tenant_memberships_user_id",
            table: "tenant_memberships",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_tenants_name",
            table: "tenants",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_tenants_status",
            table: "tenants",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_users_email",
            table: "users",
            column: "email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_security_stamp",
            table: "users",
            column: "security_stamp");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "invitations");

        migrationBuilder.DropTable(
            name: "tenant_memberships");

        migrationBuilder.DropTable(
            name: "tenants");

        migrationBuilder.DropTable(
            name: "users");
    }
}
