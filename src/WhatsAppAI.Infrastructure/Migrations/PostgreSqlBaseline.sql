DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'whatsappai') THEN
        CREATE SCHEMA whatsappai;
    END IF;
END $EF$;


CREATE TABLE IF NOT EXISTS whatsappai.ai_interactions (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    message_id uuid NOT NULL,
    model_id character varying(100) NOT NULL,
    decision character varying(20) NOT NULL,
    handoff_reason character varying(500),
    confidence double precision NOT NULL,
    input_tokens integer NOT NULL,
    output_tokens integer NOT NULL,
    latency_ms integer NOT NULL,
    response_id character varying(100),
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ai_interactions" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.ai_provider_credentials (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    provider character varying(50) NOT NULL,
    model_id character varying(100) NOT NULL,
    api_key_ref character varying(200) NOT NULL,
    system_prompt character varying(4000),
    routing_queue_ids_json TEXT,
    routing_tag_ids_json TEXT,
    max_tokens_per_response integer NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    version bigint NOT NULL,
    CONSTRAINT "PK_ai_provider_credentials" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.audit_logs (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    user_id uuid,
    action character varying(50) NOT NULL,
    entity_type character varying(50) NOT NULL,
    entity_id character varying(100),
    details character varying(2000),
    ip_address character varying(45),
    occurred_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_audit_logs" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.bot_configurations (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    mode character varying(30) NOT NULL,
    welcome_message character varying(1000),
    returning_message character varying(1000),
    offline_message character varying(1000),
    fallback_message character varying(1000),
    handoff_message character varying(1000),
    queue_transfer_message character varying(1000),
    media_message character varying(1000),
    flow_steps_json character varying(20000),
    max_tokens_per_response integer NOT NULL,
    enabled boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    version bigint NOT NULL,
    CONSTRAINT "PK_bot_configurations" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.broadcast_lists (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    message character varying(4096) NOT NULL,
    status character varying(20) NOT NULL,
    line_phone_number_id character varying(100) NOT NULL,
    queue_id uuid,
    total_count integer NOT NULL,
    sent_count integer NOT NULL,
    failed_count integer NOT NULL,
    created_by_user_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    started_at timestamp with time zone,
    finished_at timestamp with time zone,
    CONSTRAINT "PK_broadcast_lists" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.client_tags (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    color character varying(20),
    description character varying(500),
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_client_tags" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.contact_tags (
    id uuid NOT NULL,
    contact_id uuid NOT NULL,
    tag_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_contact_tags" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.contacts (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    phone_number character varying(20) NOT NULL,
    name character varying(200),
    profile_picture_url character varying(500),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    last_message_at timestamp with time zone,
    CONSTRAINT "PK_contacts" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.handoff_events (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    from_mode character varying(20) NOT NULL,
    to_mode character varying(20) NOT NULL,
    operator_user_id uuid,
    reason character varying(500) NOT NULL,
    occurred_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_handoff_events" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.knowledge_items (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    title character varying(200) NOT NULL,
    content character varying(4000) NOT NULL,
    priority integer NOT NULL,
    is_active boolean NOT NULL,
    version bigint NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    deactivated_at timestamp with time zone,
    reactivated_at timestamp with time zone,
    CONSTRAINT "PK_knowledge_items" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.model_evaluations (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    model_id character varying(100) NOT NULL,
    evaluator_user_id text NOT NULL,
    quality_score double precision NOT NULL,
    handoff_rate double precision NOT NULL,
    safety_score double precision NOT NULL,
    cost_per_1k_tokens numeric(10,4) NOT NULL,
    p95_latency_ms integer NOT NULL,
    is_approved boolean NOT NULL,
    rejection_reason character varying(500),
    rollback_model_id character varying(100),
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_model_evaluations" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.outbox_messages (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    message_id uuid NOT NULL,
    status character varying(20) NOT NULL,
    retry_count integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    processed_at timestamp with time zone,
    next_retry_at timestamp with time zone,
    last_error character varying(2000),
    CONSTRAINT "PK_outbox_messages" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.secrets (
    id uuid NOT NULL,
    key character varying(200) NOT NULL,
    encrypted_value text NOT NULL,
    tenant_id uuid,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    CONSTRAINT "PK_secrets" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.service_queues (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    color character varying(20),
    keywords character varying(500),
    sort_order integer NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_service_queues" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.subscription_plans (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    code character varying(20) NOT NULL,
    description character varying(500),
    ai_enabled boolean NOT NULL,
    openai_required boolean NOT NULL,
    ai_metrics boolean NOT NULL,
    max_operators integer,
    max_knowledge_items integer,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    CONSTRAINT "PK_subscription_plans" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.tenants (
    id uuid NOT NULL,
    name character varying(200) NOT NULL,
    slug character varying(200) NOT NULL,
    plan_id uuid NOT NULL,
    official_api_line_count integer NOT NULL DEFAULT 0,
    qr_code_line_count integer NOT NULL DEFAULT 0,
    operator_limit integer NOT NULL DEFAULT 0,
    status character varying(20) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    due_date timestamp with time zone NOT NULL,
    last_payment_at timestamp with time zone,
    activated_at timestamp with time zone,
    suspended_at timestamp with time zone,
    reactivated_at timestamp with time zone,
    closed_at timestamp with time zone,
    suspension_reason character varying(500),
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT "PK_tenants" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.usage_ledger (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    provider character varying(50) NOT NULL,
    metric character varying(50) NOT NULL,
    source_id character varying(200) NOT NULL,
    quantity bigint NOT NULL,
    unit character varying(20),
    cost_minor_units bigint,
    currency character varying(3),
    recorded_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_usage_ledger" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.users (
    id uuid NOT NULL,
    email character varying(300) NOT NULL,
    password_hash character varying(500),
    display_name character varying(200),
    is_active boolean NOT NULL,
    is_platform_admin boolean NOT NULL DEFAULT FALSE,
    "MustChangePassword" boolean NOT NULL,
    security_stamp character varying(64) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    activated_at timestamp with time zone,
    last_login_at timestamp with time zone,
    CONSTRAINT "PK_users" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.webhook_events (
    id uuid NOT NULL,
    phone_number_id character varying(100) NOT NULL,
    tenant_id uuid,
    idempotency_key character varying(500) NOT NULL,
    status character varying(20) NOT NULL,
    "RawPayloadRef" text NOT NULL,
    encrypted_payload character varying(100000) NOT NULL,
    signature character varying(200),
    error_message character varying(2000),
    retry_count integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    processed_at timestamp with time zone,
    next_retry_at timestamp with time zone,
    CONSTRAINT "PK_webhook_events" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.whatsapp_accounts (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    connection_type character varying(20) NOT NULL,
    line_number integer NOT NULL,
    waba_id character varying(100) NOT NULL,
    phone_number_id character varying(100) NOT NULL,
    access_token_ref character varying(200) NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT "PK_whatsapp_accounts" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS whatsappai.broadcast_recipients (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    broadcast_list_id uuid NOT NULL,
    contact_id uuid NOT NULL,
    status character varying(20) NOT NULL,
    error_message character varying(500),
    created_at timestamp with time zone NOT NULL,
    sent_at timestamp with time zone,
    CONSTRAINT "PK_broadcast_recipients" PRIMARY KEY (id),
    CONSTRAINT "FK_broadcast_recipients_broadcast_lists_broadcast_list_id" FOREIGN KEY (broadcast_list_id) REFERENCES whatsappai.broadcast_lists (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS whatsappai.conversations (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    contact_id uuid NOT NULL,
    phone_number_id character varying(100) NOT NULL,
    mode character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    assigned_to_user_id character varying(50),
    queue_id uuid,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    last_message_at timestamp with time zone,
    window_expires_at timestamp with time zone,
    CONSTRAINT "PK_conversations" PRIMARY KEY (id),
    CONSTRAINT "FK_conversations_contacts_contact_id" FOREIGN KEY (contact_id) REFERENCES whatsappai.contacts (id) ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS whatsappai.invitations (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    user_id uuid,
    email character varying(300) NOT NULL,
    token_hash character varying(128) NOT NULL,
    purpose character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    created_by_user_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    consumed_at timestamp with time zone,
    revoked_at timestamp with time zone,
    revoked_by_user_id uuid,
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT "PK_invitations" PRIMARY KEY (id),
    CONSTRAINT "FK_invitations_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES whatsappai.tenants (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_invitations_users_user_id" FOREIGN KEY (user_id) REFERENCES whatsappai.users (id) ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS whatsappai.tenant_memberships (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    user_id uuid NOT NULL,
    role character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    deactivated_at timestamp with time zone,
    reactivated_at timestamp with time zone,
    version bigint NOT NULL DEFAULT 0,
    assigned_connection_type character varying(20),
    assigned_line_number integer,
    assigned_lines jsonb,
    CONSTRAINT "PK_tenant_memberships" PRIMARY KEY (id),
    CONSTRAINT "FK_tenant_memberships_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES whatsappai.tenants (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_tenant_memberships_users_user_id" FOREIGN KEY (user_id) REFERENCES whatsappai.users (id) ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS whatsappai.messages (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    conversation_id uuid NOT NULL,
    contact_id uuid NOT NULL,
    external_id character varying(100),
    direction character varying(10) NOT NULL,
    status character varying(20) NOT NULL,
    type character varying(20) NOT NULL,
    content character varying(4000),
    media_id character varying(200),
    media_url character varying(500),
    caption character varying(4000),
    quoted_message_id character varying(100),
    idempotency_key character varying(200),
    created_at timestamp with time zone NOT NULL,
    sent_at timestamp with time zone,
    delivered_at timestamp with time zone,
    read_at timestamp with time zone,
    failed_at timestamp with time zone,
    failure_reason character varying(2000),
    processed_by_ai boolean NOT NULL,
    ai_retry_count integer NOT NULL DEFAULT 0,
    next_ai_retry_at timestamp with time zone,
    CONSTRAINT "PK_messages" PRIMARY KEY (id),
    CONSTRAINT "FK_messages_contacts_contact_id" FOREIGN KEY (contact_id) REFERENCES whatsappai.contacts (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_messages_conversations_conversation_id" FOREIGN KEY (conversation_id) REFERENCES whatsappai.conversations (id) ON DELETE RESTRICT
);


CREATE INDEX IF NOT EXISTS "IX_ai_interactions_tenant_id" ON whatsappai.ai_interactions (tenant_id);


CREATE INDEX IF NOT EXISTS "IX_ai_interactions_tenant_id_conversation_id_created_at" ON whatsappai.ai_interactions (tenant_id, conversation_id, created_at);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_ai_provider_credentials_tenant_id_provider" ON whatsappai.ai_provider_credentials (tenant_id, provider);


CREATE INDEX IF NOT EXISTS "IX_audit_logs_tenant_id_entity_type_entity_id" ON whatsappai.audit_logs (tenant_id, entity_type, entity_id);


CREATE INDEX IF NOT EXISTS "IX_audit_logs_tenant_id_occurred_at" ON whatsappai.audit_logs (tenant_id, occurred_at);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_bot_configurations_tenant_id" ON whatsappai.bot_configurations (tenant_id);


CREATE INDEX IF NOT EXISTS "IX_broadcast_lists_tenant_id" ON whatsappai.broadcast_lists (tenant_id);


CREATE INDEX IF NOT EXISTS "IX_broadcast_lists_tenant_id_status" ON whatsappai.broadcast_lists (tenant_id, status);


CREATE INDEX IF NOT EXISTS "IX_broadcast_recipients_broadcast_list_id" ON whatsappai.broadcast_recipients (broadcast_list_id);


CREATE INDEX IF NOT EXISTS "IX_broadcast_recipients_broadcast_list_id_status" ON whatsappai.broadcast_recipients (broadcast_list_id, status);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_broadcast_recipients_tenant_id_broadcast_list_id_contact_id" ON whatsappai.broadcast_recipients (tenant_id, broadcast_list_id, contact_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_client_tags_tenant_id_name" ON whatsappai.client_tags (tenant_id, name);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_contact_tags_contact_id_tag_id" ON whatsappai.contact_tags (contact_id, tag_id);


CREATE INDEX IF NOT EXISTS "IX_contacts_tenant_id" ON whatsappai.contacts (tenant_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_contacts_tenant_id_phone_number" ON whatsappai.contacts (tenant_id, phone_number);


CREATE INDEX IF NOT EXISTS "IX_conversations_contact_id" ON whatsappai.conversations (contact_id);


CREATE INDEX IF NOT EXISTS "IX_conversations_last_message_at" ON whatsappai.conversations (last_message_at);


CREATE INDEX IF NOT EXISTS "IX_conversations_tenant_id" ON whatsappai.conversations (tenant_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_conversations_tenant_id_contact_id_phone_number_id" ON whatsappai.conversations (tenant_id, contact_id, phone_number_id);


CREATE INDEX IF NOT EXISTS "IX_handoff_events_tenant_id_conversation_id_occurred_at" ON whatsappai.handoff_events (tenant_id, conversation_id, occurred_at);


CREATE INDEX IF NOT EXISTS "IX_invitations_expires_at" ON whatsappai.invitations (expires_at);


CREATE INDEX IF NOT EXISTS "IX_invitations_tenant_id_email_status" ON whatsappai.invitations (tenant_id, email, status);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_invitations_token_hash" ON whatsappai.invitations (token_hash);


CREATE INDEX IF NOT EXISTS "IX_invitations_user_id" ON whatsappai.invitations (user_id);


CREATE INDEX IF NOT EXISTS "IX_knowledge_items_tenant_id" ON whatsappai.knowledge_items (tenant_id);


CREATE INDEX IF NOT EXISTS "IX_knowledge_items_tenant_id_is_active" ON whatsappai.knowledge_items (tenant_id, is_active);


CREATE INDEX IF NOT EXISTS "IX_messages_contact_id" ON whatsappai.messages (contact_id);


CREATE INDEX IF NOT EXISTS "IX_messages_conversation_id" ON whatsappai.messages (conversation_id);


CREATE INDEX IF NOT EXISTS "IX_messages_external_id" ON whatsappai.messages (external_id);


CREATE INDEX IF NOT EXISTS "IX_messages_idempotency_key" ON whatsappai.messages (idempotency_key);


CREATE INDEX IF NOT EXISTS "IX_messages_tenant_id" ON whatsappai.messages (tenant_id);


CREATE INDEX IF NOT EXISTS "IX_messages_tenant_id_conversation_id_created_at" ON whatsappai.messages (tenant_id, conversation_id, created_at);


CREATE INDEX IF NOT EXISTS "IX_messages_processed_by_ai_next_ai_retry_at" ON whatsappai.messages (processed_by_ai, next_ai_retry_at);


CREATE INDEX IF NOT EXISTS "IX_model_evaluations_tenant_id_is_approved_created_at" ON whatsappai.model_evaluations (tenant_id, is_approved, created_at);


CREATE INDEX IF NOT EXISTS "IX_outbox_messages_status_next_retry_at" ON whatsappai.outbox_messages (status, next_retry_at);


CREATE INDEX IF NOT EXISTS "IX_outbox_messages_tenant_id" ON whatsappai.outbox_messages (tenant_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_secrets_key_tenant_id" ON whatsappai.secrets (key, tenant_id);


CREATE INDEX IF NOT EXISTS "IX_secrets_tenant_id" ON whatsappai.secrets (tenant_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_service_queues_tenant_id_name" ON whatsappai.service_queues (tenant_id, name);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_subscription_plans_code" ON whatsappai.subscription_plans (code);


CREATE INDEX IF NOT EXISTS "IX_subscription_plans_is_active" ON whatsappai.subscription_plans (is_active);


CREATE INDEX IF NOT EXISTS "IX_tenant_memberships_status" ON whatsappai.tenant_memberships (status);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_tenant_memberships_tenant_id_user_id" ON whatsappai.tenant_memberships (tenant_id, user_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_tenant_memberships_user_id" ON whatsappai.tenant_memberships (user_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_tenants_name" ON whatsappai.tenants (name);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_tenants_slug" ON whatsappai.tenants (slug);


CREATE INDEX IF NOT EXISTS "IX_tenants_status" ON whatsappai.tenants (status);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_usage_ledger_tenant_id_provider_metric_source_id" ON whatsappai.usage_ledger (tenant_id, provider, metric, source_id);


CREATE INDEX IF NOT EXISTS "IX_usage_ledger_tenant_id_recorded_at" ON whatsappai.usage_ledger (tenant_id, recorded_at);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_users_email" ON whatsappai.users (email);


CREATE INDEX IF NOT EXISTS "IX_users_security_stamp" ON whatsappai.users (security_stamp);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_webhook_events_idempotency_key" ON whatsappai.webhook_events (idempotency_key);


CREATE INDEX IF NOT EXISTS "IX_webhook_events_next_retry_at" ON whatsappai.webhook_events (next_retry_at);


CREATE INDEX IF NOT EXISTS "IX_webhook_events_status" ON whatsappai.webhook_events (status);


CREATE INDEX IF NOT EXISTS "IX_webhook_events_status_created_at" ON whatsappai.webhook_events (status, created_at);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_whatsapp_accounts_phone_number_id" ON whatsappai.whatsapp_accounts (phone_number_id);


CREATE INDEX IF NOT EXISTS "IX_whatsapp_accounts_tenant_id" ON whatsappai.whatsapp_accounts (tenant_id);


CREATE UNIQUE INDEX IF NOT EXISTS "IX_whatsapp_accounts_tenant_id_connection_type_line_number" ON whatsappai.whatsapp_accounts (tenant_id, connection_type, line_number);


