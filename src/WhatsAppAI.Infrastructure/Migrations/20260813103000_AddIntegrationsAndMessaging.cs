using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddIntegrationsAndMessaging : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "secrets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                encrypted_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_secrets", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "whatsapp_accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                waba_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                phone_number_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                access_token_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                version = table.Column<uint>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_whatsapp_accounts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "webhook_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                phone_number_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                idempotency_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                encrypted_payload = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                signature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                processed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                next_retry_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_webhook_events", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_secrets_key_tenant_id",
            table: "secrets",
            columns: new[] { "key", "tenant_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_secrets_tenant_id",
            table: "secrets",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_webhook_events_idempotency_key",
            table: "webhook_events",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_webhook_events_next_retry_at",
            table: "webhook_events",
            column: "next_retry_at",
            filter: "status = 'Failed'");

        migrationBuilder.CreateIndex(
            name: "ix_webhook_events_status",
            table: "webhook_events",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_webhook_events_status_created_at",
            table: "webhook_events",
            columns: new[] { "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_whatsapp_accounts_phone_number_id",
            table: "whatsapp_accounts",
            column: "phone_number_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_whatsapp_accounts_tenant_id",
            table: "whatsapp_accounts",
            column: "tenant_id",
            unique: true,
            filter: "is_active = true");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "secrets");

        migrationBuilder.DropTable(
            name: "webhook_events");

        migrationBuilder.DropTable(
            name: "whatsapp_accounts");
    }
}
