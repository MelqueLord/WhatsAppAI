using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppTemplateManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "whatsapp_business_accounts",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waba_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_business_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_message_templates",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whatsapp_business_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meta_template_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    effective_category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    parameter_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    body_text = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    footer_text = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    body_examples_json = table.Column<string>(type: "jsonb", nullable: false),
                    body_parameter_count = table.Column<int>(type: "integer", nullable: false),
                    components_json = table.Column<string>(type: "jsonb", nullable: false),
                    review_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider_raw_status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recommendation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_inbox_compatible = table.Column<bool>(type: "boolean", nullable: false),
                    is_broadcast_compatible = table.Column<bool>(type: "boolean", nullable: false),
                    provider_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_message_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_whatsapp_message_templates_whatsapp_business_accounts_whats~",
                        column: x => x.whatsapp_business_account_id,
                        principalSchema: "whatsappai",
                        principalTable: "whatsapp_business_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT waba_id
                        FROM whatsappai.whatsapp_accounts
                        WHERE connection_type = 'OfficialApi' AND waba_id <> ''
                        GROUP BY waba_id
                        HAVING COUNT(DISTINCT tenant_id) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate WhatsApp templates: a WABA is associated with more than one tenant.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                INSERT INTO whatsappai.whatsapp_business_accounts (id, tenant_id, waba_id, created_at, version)
                SELECT md5(tenant_id::text || ':' || waba_id)::uuid, tenant_id, waba_id, NOW(), 0
                FROM whatsappai.whatsapp_accounts
                WHERE connection_type = 'OfficialApi' AND waba_id <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM whatsappai.whatsapp_business_accounts AS existing
                      WHERE existing.waba_id = whatsapp_accounts.waba_id
                  )
                GROUP BY tenant_id, waba_id

                UPDATE whatsappai.whatsapp_accounts AS line
                SET whatsapp_business_account_id = waba.id
                FROM whatsappai.whatsapp_business_accounts AS waba
                WHERE line.connection_type = 'OfficialApi'
                  AND line.tenant_id = waba.tenant_id
                  AND line.waba_id = waba.waba_id;
                """);

            migrationBuilder.CreateTable(
                name: "whatsapp_template_submissions",
                schema: "whatsappai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whatsapp_business_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whatsapp_message_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_whatsapp_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    claim_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_error_category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_template_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_whatsapp_template_submissions_whatsapp_accounts_source_what~",
                        column: x => x.source_whatsapp_account_id,
                        principalSchema: "whatsappai",
                        principalTable: "whatsapp_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_template_submissions_whatsapp_business_accounts_wh~",
                        column: x => x.whatsapp_business_account_id,
                        principalSchema: "whatsappai",
                        principalTable: "whatsapp_business_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_template_submissions_whatsapp_message_templates_wh~",
                        column: x => x.whatsapp_message_template_id,
                        principalSchema: "whatsappai",
                        principalTable: "whatsapp_message_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_accounts_tenant_id_whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_accounts",
                columns: new[] { "tenant_id", "whatsapp_business_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_accounts_whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_accounts",
                column: "whatsapp_business_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_business_accounts_tenant_id_id",
                schema: "whatsappai",
                table: "whatsapp_business_accounts",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_business_accounts_waba_id",
                schema: "whatsappai",
                table: "whatsapp_business_accounts",
                column: "waba_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_message_templates_tenant_id_whatsapp_business_acc~1",
                schema: "whatsappai",
                table: "whatsapp_message_templates",
                columns: new[] { "tenant_id", "whatsapp_business_account_id", "name", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_message_templates_tenant_id_whatsapp_business_acco~",
                schema: "whatsappai",
                table: "whatsapp_message_templates",
                columns: new[] { "tenant_id", "whatsapp_business_account_id", "meta_template_id" },
                unique: true,
                filter: "meta_template_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_message_templates_whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_message_templates",
                column: "whatsapp_business_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_submissions_source_whatsapp_account_id",
                schema: "whatsappai",
                table: "whatsapp_template_submissions",
                column: "source_whatsapp_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_submissions_status_next_attempt_at",
                schema: "whatsappai",
                table: "whatsapp_template_submissions",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_submissions_tenant_id_idempotency_key",
                schema: "whatsappai",
                table: "whatsapp_template_submissions",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_submissions_whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_template_submissions",
                column: "whatsapp_business_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_template_submissions_whatsapp_message_template_id",
                schema: "whatsappai",
                table: "whatsapp_template_submissions",
                column: "whatsapp_message_template_id");

            migrationBuilder.AddForeignKey(
                name: "FK_whatsapp_accounts_whatsapp_business_accounts_whatsapp_busin~",
                schema: "whatsappai",
                table: "whatsapp_accounts",
                column: "whatsapp_business_account_id",
                principalSchema: "whatsappai",
                principalTable: "whatsapp_business_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_whatsapp_accounts_whatsapp_business_accounts_whatsapp_busin~",
                schema: "whatsappai",
                table: "whatsapp_accounts");

            migrationBuilder.DropTable(
                name: "whatsapp_template_submissions",
                schema: "whatsappai");

            migrationBuilder.DropTable(
                name: "whatsapp_message_templates",
                schema: "whatsappai");

            migrationBuilder.DropTable(
                name: "whatsapp_business_accounts",
                schema: "whatsappai");

            migrationBuilder.DropIndex(
                name: "IX_whatsapp_accounts_tenant_id_whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_accounts");

            migrationBuilder.DropIndex(
                name: "IX_whatsapp_accounts_whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "whatsapp_business_account_id",
                schema: "whatsappai",
                table: "whatsapp_accounts");
        }
    }
}
