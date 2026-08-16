using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_interactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    conversation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    message_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    model_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    decision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    handoff_reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    confidence = table.Column<double>(type: "REAL", nullable: false),
                    input_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    output_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    latency_ms = table.Column<int>(type: "INTEGER", nullable: false),
                    response_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_interactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_provider_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    model_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    api_key_ref = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    version = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_provider_credentials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ip_address = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bot_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    mode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    welcome_message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    offline_message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    fallback_message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    max_tokens_per_response = table.Column<int>(type: "INTEGER", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    version = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "client_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contact_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    contact_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tag_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    phone_number = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    profile_picture_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_message_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "handoff_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    conversation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    from_mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    to_mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    operator_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handoff_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    version = table.Column<uint>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deactivated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reactivated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "model_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    model_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    evaluator_user_id = table.Column<string>(type: "TEXT", nullable: false),
                    quality_score = table.Column<double>(type: "REAL", nullable: false),
                    handoff_rate = table.Column<double>(type: "REAL", nullable: false),
                    safety_score = table.Column<double>(type: "REAL", nullable: false),
                    cost_per_1k_tokens = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    p95_latency_ms = table.Column<int>(type: "INTEGER", nullable: false),
                    is_approved = table.Column<bool>(type: "INTEGER", nullable: false),
                    rejection_reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    rollback_model_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_evaluations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    message_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    retry_count = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "secrets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    encrypted_value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secrets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ai_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    openai_required = table.Column<bool>(type: "INTEGER", nullable: false),
                    ai_metrics = table.Column<bool>(type: "INTEGER", nullable: false),
                    max_operators = table.Column<int>(type: "INTEGER", nullable: true),
                    max_knowledge_items = table.Column<int>(type: "INTEGER", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    plan_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    activated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    suspended_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reactivated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    suspension_reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    version = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usage_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    metric = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    source_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    cost_minor_units = table.Column<long>(type: "INTEGER", nullable: true),
                    currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    recorded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_ledger", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_platform_admin = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    security_stamp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    activated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    phone_number_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    idempotency_key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RawPayloadRef = table.Column<string>(type: "TEXT", nullable: false),
                    encrypted_payload = table.Column<string>(type: "TEXT", maxLength: 100000, nullable: false),
                    signature = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    retry_count = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    waba_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    phone_number_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    access_token_ref = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    version = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    contact_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    phone_number_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    assigned_to_user_id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    version = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 1u),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_message_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    window_expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversations_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    email = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    purpose = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invitations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    deactivated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reactivated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    version = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    conversation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    contact_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    direction = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    media_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    media_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    caption = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    quoted_message_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failure_reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    processed_by_ai = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_tenant_id",
                table: "ai_interactions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_tenant_id_conversation_id_created_at",
                table: "ai_interactions",
                columns: new[] { "tenant_id", "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_credentials_tenant_id_provider",
                table: "ai_provider_credentials",
                columns: new[] { "tenant_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_bot_configurations_tenant_id",
                table: "bot_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_tags_tenant_id_name",
                table: "client_tags",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_tags_contact_id_tag_id",
                table: "contact_tags",
                columns: new[] { "contact_id", "tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contacts_tenant_id",
                table: "contacts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_tenant_id_phone_number",
                table: "contacts",
                columns: new[] { "tenant_id", "phone_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_contact_id",
                table: "conversations",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_last_message_at",
                table: "conversations",
                column: "last_message_at");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_tenant_id",
                table: "conversations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_tenant_id_contact_id_phone_number_id",
                table: "conversations",
                columns: new[] { "tenant_id", "contact_id", "phone_number_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_handoff_events_tenant_id_conversation_id_occurred_at",
                table: "handoff_events",
                columns: new[] { "tenant_id", "conversation_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_expires_at",
                table: "invitations",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_tenant_id_email_status",
                table: "invitations",
                columns: new[] { "tenant_id", "email", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_token_hash",
                table: "invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_user_id",
                table: "invitations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_items_tenant_id",
                table: "knowledge_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_items_tenant_id_is_active",
                table: "knowledge_items",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_contact_id",
                table: "messages",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_conversation_id",
                table: "messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_external_id",
                table: "messages",
                column: "external_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_idempotency_key",
                table: "messages",
                column: "idempotency_key");

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id",
                table: "messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id_conversation_id_created_at",
                table: "messages",
                columns: new[] { "tenant_id", "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_model_evaluations_tenant_id_is_approved_created_at",
                table: "model_evaluations",
                columns: new[] { "tenant_id", "is_approved", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_status_next_retry_at",
                table: "outbox_messages",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_tenant_id",
                table: "outbox_messages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_secrets_key_tenant_id",
                table: "secrets",
                columns: new[] { "key", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_secrets_tenant_id",
                table: "secrets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_code",
                table: "subscription_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_is_active",
                table: "subscription_plans",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_status",
                table: "tenant_memberships",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_tenant_id_user_id",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_user_id",
                table: "tenant_memberships",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_name",
                table: "tenants",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_status",
                table: "tenants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_usage_ledger_tenant_id_provider_metric_source_id",
                table: "usage_ledger",
                columns: new[] { "tenant_id", "provider", "metric", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usage_ledger_tenant_id_recorded_at",
                table: "usage_ledger",
                columns: new[] { "tenant_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_security_stamp",
                table: "users",
                column: "security_stamp");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_idempotency_key",
                table: "webhook_events",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_next_retry_at",
                table: "webhook_events",
                column: "next_retry_at");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_status",
                table: "webhook_events",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_status_created_at",
                table: "webhook_events",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_accounts_phone_number_id",
                table: "whatsapp_accounts",
                column: "phone_number_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_accounts_tenant_id",
                table: "whatsapp_accounts",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_interactions");

            migrationBuilder.DropTable(
                name: "ai_provider_credentials");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "bot_configurations");

            migrationBuilder.DropTable(
                name: "client_tags");

            migrationBuilder.DropTable(
                name: "contact_tags");

            migrationBuilder.DropTable(
                name: "handoff_events");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "knowledge_items");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "model_evaluations");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "secrets");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "tenant_memberships");

            migrationBuilder.DropTable(
                name: "usage_ledger");

            migrationBuilder.DropTable(
                name: "webhook_events");

            migrationBuilder.DropTable(
                name: "whatsapp_accounts");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "contacts");
        }
    }
}
