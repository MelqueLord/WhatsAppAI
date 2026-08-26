using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                suspended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                suspension_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                version = table.Column<uint>(type: "bigint", nullable: false, defaultValue: 0u)
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
                email = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                password_hash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                display_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                security_stamp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                version = table.Column<uint>(type: "bigint", nullable: false, defaultValue: 0u)
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
                email = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                token_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                purpose = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                version = table.Column<uint>(type: "bigint", nullable: false, defaultValue: 0u)
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

        migrationBuilder.CreateTable(
            name: "secrets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                encrypted_value = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                waba_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                phone_number_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                access_token_ref = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                version = table.Column<uint>(type: "bigint", nullable: false, defaultValue: 0u)
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
                phone_number_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                idempotency_key = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                encrypted_payload = table.Column<string>(type: "text", maxLength: 100000, nullable: false),
                signature = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                error_message = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                retry_count = table.Column<int>(type: "int", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_webhook_events", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "contacts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                phone_number = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                profile_picture_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_contacts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "conversations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                phone_number_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                mode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                assigned_to_user_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                version = table.Column<uint>(type: "bigint", nullable: false, defaultValue: 1u),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                window_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_conversations", x => x.id);
                table.ForeignKey(
                    name: "fk_conversations_contacts_contact_id",
                    column: x => x.contact_id,
                    principalTable: "contacts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                external_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                direction = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                content = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                media_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                media_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                caption = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                quoted_message_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                idempotency_key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                failure_reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                processed_by_ai = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_messages", x => x.id);
                table.ForeignKey(
                    name: "fk_messages_contacts_contact_id",
                    column: x => x.contact_id,
                    principalTable: "contacts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_messages_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalTable: "conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "handoff_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                from_mode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                to_mode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_handoff_events", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                retry_count = table.Column<int>(type: "int", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_error = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "ai_provider_credentials",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                model_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                api_key_ref = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                version = table.Column<uint>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ai_provider_credentials", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "ai_interactions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                model_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                decision = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                handoff_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                confidence = table.Column<double>(type: "double precision", nullable: false),
                input_tokens = table.Column<int>(type: "int", nullable: false),
                output_tokens = table.Column<int>(type: "int", nullable: false),
                latency_ms = table.Column<int>(type: "int", nullable: false),
                response_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ai_interactions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "usage_ledger",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                metric = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                source_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                quantity = table.Column<long>(type: "bigint", nullable: false),
                unit = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                cost_minor_units = table.Column<long>(type: "bigint", nullable: true),
                currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true),
                recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_usage_ledger", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "model_evaluations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                model_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                evaluator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                quality_score = table.Column<double>(type: "double precision", nullable: false),
                handoff_rate = table.Column<double>(type: "double precision", nullable: false),
                safety_score = table.Column<double>(type: "double precision", nullable: false),
                cost_per_1k_tokens = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                p95_latency_ms = table.Column<int>(type: "int", nullable: false),
                is_approved = table.Column<bool>(type: "boolean", nullable: false),
                rejection_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                rollback_model_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_model_evaluations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "knowledge_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                content = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                priority = table.Column<int>(type: "int", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                version = table.Column<uint>(type: "bigint", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_knowledge_items", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "client_tags",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                color = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_client_tags", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "contact_tags",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_contact_tags", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "bot_configurations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                mode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                welcome_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                offline_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                fallback_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                max_tokens_per_response = table.Column<int>(type: "int", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                version = table.Column<uint>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bot_configurations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                entity_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                entity_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                details = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_logs", x => x.id);
            });

        // --- Indexes ---

        migrationBuilder.CreateIndex(name: "ix_tenants_name", table: "tenants", column: "name", unique: true);
        migrationBuilder.CreateIndex(name: "ix_tenants_slug", table: "tenants", column: "slug", unique: true);
        migrationBuilder.CreateIndex(name: "ix_tenants_status", table: "tenants", column: "status");

        migrationBuilder.CreateIndex(name: "ix_users_email", table: "users", column: "email", unique: true);
        migrationBuilder.CreateIndex(name: "ix_users_security_stamp", table: "users", column: "security_stamp");

        migrationBuilder.CreateIndex(name: "ix_tenant_memberships_tenant_user", table: "tenant_memberships", columns: new[] { "tenant_id", "user_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_tenant_memberships_user_id", table: "tenant_memberships", column: "user_id");
        migrationBuilder.CreateIndex(name: "ix_tenant_memberships_status", table: "tenant_memberships", column: "status");

        migrationBuilder.CreateIndex(name: "ix_invitations_token_hash", table: "invitations", column: "token_hash", unique: true);
        migrationBuilder.CreateIndex(name: "ix_invitations_tenant_email_status", table: "invitations", columns: new[] { "tenant_id", "email", "status" });
        migrationBuilder.CreateIndex(name: "ix_invitations_expires_at", table: "invitations", column: "expires_at");

        migrationBuilder.CreateIndex(name: "ix_secrets_key_tenant", table: "secrets", columns: new[] { "key", "tenant_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_secrets_tenant_id", table: "secrets", column: "tenant_id");

        migrationBuilder.CreateIndex(name: "ix_whatsapp_accounts_phone", table: "whatsapp_accounts", column: "phone_number_id", unique: true);
        migrationBuilder.CreateIndex(name: "ix_whatsapp_accounts_tenant", table: "whatsapp_accounts", column: "tenant_id");

        migrationBuilder.CreateIndex(name: "ix_webhook_events_idempotency", table: "webhook_events", column: "idempotency_key", unique: true);
        migrationBuilder.CreateIndex(name: "ix_webhook_events_status", table: "webhook_events", column: "status");
        migrationBuilder.CreateIndex(name: "ix_webhook_events_next_retry", table: "webhook_events", column: "next_retry_at");
        migrationBuilder.CreateIndex(name: "ix_webhook_events_status_created", table: "webhook_events", columns: new[] { "status", "created_at" });

        migrationBuilder.CreateIndex(name: "ix_contacts_tenant_phone", table: "contacts", columns: new[] { "tenant_id", "phone_number" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_contacts_tenant_id", table: "contacts", column: "tenant_id");

        migrationBuilder.CreateIndex(name: "ix_conversations_contact", table: "conversations", column: "contact_id");
        migrationBuilder.CreateIndex(name: "ix_conversations_last_msg", table: "conversations", column: "last_message_at");
        migrationBuilder.CreateIndex(name: "ix_conversations_tenant", table: "conversations", column: "tenant_id");
        migrationBuilder.CreateIndex(name: "ix_conversations_tenant_contact_phone", table: "conversations", columns: new[] { "tenant_id", "contact_id", "phone_number_id" }, unique: true);

        migrationBuilder.CreateIndex(name: "ix_messages_tenant_conv_created", table: "messages", columns: new[] { "tenant_id", "conversation_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_messages_external_id", table: "messages", column: "external_id");
        migrationBuilder.CreateIndex(name: "ix_messages_idempotency", table: "messages", column: "idempotency_key");
        migrationBuilder.CreateIndex(name: "ix_messages_tenant_id", table: "messages", column: "tenant_id");

        migrationBuilder.CreateIndex(name: "ix_handoff_tenant_conv_occurred", table: "handoff_events", columns: new[] { "tenant_id", "conversation_id", "occurred_at" });

        migrationBuilder.CreateIndex(name: "ix_outbox_status_retry", table: "outbox_messages", columns: new[] { "status", "next_retry_at" });
        migrationBuilder.CreateIndex(name: "ix_outbox_tenant", table: "outbox_messages", column: "tenant_id");

        migrationBuilder.CreateIndex(name: "ix_ai_creds_tenant_provider", table: "ai_provider_credentials", columns: new[] { "tenant_id", "provider" }, unique: true);

        migrationBuilder.CreateIndex(name: "ix_ai_interactions_tenant_conv", table: "ai_interactions", columns: new[] { "tenant_id", "conversation_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_ai_interactions_tenant", table: "ai_interactions", column: "tenant_id");

        migrationBuilder.CreateIndex(name: "ix_usage_tenant_provider_metric", table: "usage_ledger", columns: new[] { "tenant_id", "provider", "metric", "source_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_usage_tenant_recorded", table: "usage_ledger", columns: new[] { "tenant_id", "recorded_at" });

        migrationBuilder.CreateIndex(name: "ix_model_eval_tenant_approved", table: "model_evaluations", columns: new[] { "tenant_id", "is_approved", "created_at" });

        migrationBuilder.CreateIndex(name: "ix_knowledge_tenant_active", table: "knowledge_items", columns: new[] { "tenant_id", "is_active" });
        migrationBuilder.CreateIndex(name: "ix_knowledge_tenant", table: "knowledge_items", column: "tenant_id");

        migrationBuilder.CreateIndex(name: "ix_client_tags_tenant_name", table: "client_tags", columns: new[] { "tenant_id", "name" }, unique: true);

        migrationBuilder.CreateIndex(name: "ix_contact_tags_contact_tag", table: "contact_tags", columns: new[] { "contact_id", "tag_id" }, unique: true);

        migrationBuilder.CreateIndex(name: "ix_bot_config_tenant", table: "bot_configurations", column: "tenant_id", unique: true);

        migrationBuilder.CreateIndex(name: "ix_audit_tenant_occurred", table: "audit_logs", columns: new[] { "tenant_id", "occurred_at" });
        migrationBuilder.CreateIndex(name: "ix_audit_tenant_entity", table: "audit_logs", columns: new[] { "tenant_id", "entity_type", "entity_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_logs");
        migrationBuilder.DropTable(name: "bot_configurations");
        migrationBuilder.DropTable(name: "contact_tags");
        migrationBuilder.DropTable(name: "client_tags");
        migrationBuilder.DropTable(name: "knowledge_items");
        migrationBuilder.DropTable(name: "model_evaluations");
        migrationBuilder.DropTable(name: "usage_ledger");
        migrationBuilder.DropTable(name: "ai_interactions");
        migrationBuilder.DropTable(name: "ai_provider_credentials");
        migrationBuilder.DropTable(name: "outbox_messages");
        migrationBuilder.DropTable(name: "handoff_events");
        migrationBuilder.DropTable(name: "messages");
        migrationBuilder.DropTable(name: "conversations");
        migrationBuilder.DropTable(name: "contacts");
        migrationBuilder.DropTable(name: "webhook_events");
        migrationBuilder.DropTable(name: "whatsapp_accounts");
        migrationBuilder.DropTable(name: "secrets");
        migrationBuilder.DropTable(name: "invitations");
        migrationBuilder.DropTable(name: "tenant_memberships");
        migrationBuilder.DropTable(name: "tenants");
        migrationBuilder.DropTable(name: "users");
    }
}
