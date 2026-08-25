-- ============================================================
-- WhatsAppAI — Schema completo para Supabase
-- Cria o schema "whatsappai" que o EF usa no PostgreSQL
-- Seguro: IF NOT EXISTS em tudo, não apaga dados existentes
-- ============================================================

CREATE SCHEMA IF NOT EXISTS whatsappai;

-- ─── TABELAS INDEPENDENTES ────────────────────────────────

CREATE TABLE IF NOT EXISTS whatsappai.subscription_plans (
    id                  uuid         NOT NULL,
    name                varchar(100) NOT NULL,
    code                varchar(20)  NOT NULL,
    description         varchar(500),
    ai_enabled          boolean      NOT NULL DEFAULT false,
    openai_required     boolean      NOT NULL DEFAULT false,
    ai_metrics          boolean      NOT NULL DEFAULT false,
    max_operators       integer,
    max_knowledge_items integer,
    is_active           boolean      NOT NULL DEFAULT false,
    created_at          timestamptz  NOT NULL,
    updated_at          timestamptz,
    CONSTRAINT pk_subscription_plans PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.tenants (
    id                      uuid         NOT NULL,
    name                    varchar(200) NOT NULL,
    slug                    varchar(200) NOT NULL,
    plan_id                 uuid         NOT NULL,
    status                  varchar(20)  NOT NULL,
    created_at              timestamptz  NOT NULL,
    activated_at            timestamptz,
    suspended_at            timestamptz,
    reactivated_at          timestamptz,
    closed_at               timestamptz,
    suspension_reason       varchar(500),
    version                 bigint       NOT NULL DEFAULT 0,
    due_date                timestamptz  NOT NULL DEFAULT NOW(),
    official_api_line_count integer      NOT NULL DEFAULT 0,
    qr_code_line_count      integer      NOT NULL DEFAULT 0,
    operator_limit          integer      NOT NULL DEFAULT 0,
    CONSTRAINT pk_tenants PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.users (
    id                   uuid         NOT NULL,
    email                varchar(300) NOT NULL,
    password_hash        varchar(500),
    display_name         varchar(200),
    is_active            boolean      NOT NULL DEFAULT false,
    is_platform_admin    boolean      NOT NULL DEFAULT false,
    security_stamp       varchar(64)  NOT NULL,
    created_at           timestamptz  NOT NULL,
    activated_at         timestamptz,
    last_login_at        timestamptz,
    "MustChangePassword" boolean      NOT NULL DEFAULT false,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.secrets (
    id              uuid         NOT NULL,
    key             varchar(200) NOT NULL,
    encrypted_value varchar(2000) NOT NULL,
    tenant_id       uuid,
    created_at      timestamptz  NOT NULL,
    updated_at      timestamptz,
    CONSTRAINT pk_secrets PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.contacts (
    id                  uuid        NOT NULL,
    tenant_id           uuid        NOT NULL,
    phone_number        varchar(20) NOT NULL,
    name                varchar(200),
    profile_picture_url varchar(500),
    created_at          timestamptz NOT NULL,
    updated_at          timestamptz,
    last_message_at     timestamptz,
    CONSTRAINT pk_contacts PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.outbox_messages (
    id            uuid        NOT NULL,
    tenant_id     uuid        NOT NULL,
    message_id    uuid        NOT NULL,
    status        varchar(20) NOT NULL,
    retry_count   integer     NOT NULL DEFAULT 0,
    created_at    timestamptz NOT NULL,
    processed_at  timestamptz,
    next_retry_at timestamptz,
    last_error    varchar(2000),
    CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.ai_provider_credentials (
    id                      uuid         NOT NULL,
    tenant_id               uuid         NOT NULL,
    provider                varchar(50)  NOT NULL,
    model_id                varchar(100) NOT NULL,
    api_key_ref             varchar(200) NOT NULL,
    is_active               boolean      NOT NULL DEFAULT false,
    created_at              timestamptz  NOT NULL,
    updated_at              timestamptz,
    version                 bigint       NOT NULL DEFAULT 0,
    system_prompt           varchar(4000),
    max_tokens_per_response integer      NOT NULL DEFAULT 500,
    routing_queue_ids_json  text,
    routing_tag_ids_json    text,
    CONSTRAINT pk_ai_provider_credentials PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.ai_interactions (
    id              uuid             NOT NULL,
    tenant_id       uuid             NOT NULL,
    conversation_id uuid             NOT NULL,
    message_id      uuid             NOT NULL,
    model_id        varchar(100)     NOT NULL,
    decision        varchar(20)      NOT NULL,
    handoff_reason  varchar(500),
    confidence      double precision NOT NULL DEFAULT 0,
    input_tokens    integer          NOT NULL DEFAULT 0,
    output_tokens   integer          NOT NULL DEFAULT 0,
    latency_ms      integer          NOT NULL DEFAULT 0,
    response_id     varchar(100),
    created_at      timestamptz      NOT NULL,
    CONSTRAINT pk_ai_interactions PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.usage_ledger (
    id               uuid             NOT NULL,
    tenant_id        uuid             NOT NULL,
    provider         varchar(50)      NOT NULL,
    metric           varchar(50)      NOT NULL,
    source_id        varchar(200)     NOT NULL,
    quantity         bigint           NOT NULL DEFAULT 0,
    unit             varchar(20),
    cost_minor_units bigint,
    currency         varchar(3),
    recorded_at      timestamptz      NOT NULL,
    CONSTRAINT pk_usage_ledger PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.model_evaluations (
    id                 uuid             NOT NULL,
    tenant_id          uuid             NOT NULL,
    model_id           varchar(100)     NOT NULL,
    evaluator_user_id  text             NOT NULL,
    quality_score      double precision NOT NULL DEFAULT 0,
    handoff_rate       double precision NOT NULL DEFAULT 0,
    safety_score       double precision NOT NULL DEFAULT 0,
    cost_per_1k_tokens numeric(10,4)    NOT NULL DEFAULT 0,
    p95_latency_ms     integer          NOT NULL DEFAULT 0,
    is_approved        boolean          NOT NULL DEFAULT false,
    rejection_reason   varchar(500),
    rollback_model_id  varchar(100),
    created_at         timestamptz      NOT NULL,
    CONSTRAINT pk_model_evaluations PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.knowledge_items (
    id             uuid         NOT NULL,
    tenant_id      uuid         NOT NULL,
    title          varchar(200) NOT NULL,
    content        varchar(4000) NOT NULL,
    priority       integer      NOT NULL DEFAULT 0,
    is_active      boolean      NOT NULL DEFAULT false,
    version        bigint       NOT NULL DEFAULT 0,
    created_at     timestamptz  NOT NULL,
    updated_at     timestamptz,
    deactivated_at timestamptz,
    reactivated_at timestamptz,
    CONSTRAINT pk_knowledge_items PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.client_tags (
    id          uuid         NOT NULL,
    tenant_id   uuid         NOT NULL,
    name        varchar(100) NOT NULL,
    color       varchar(20),
    description varchar(500),
    is_active   boolean      NOT NULL DEFAULT false,
    created_at  timestamptz  NOT NULL,
    CONSTRAINT pk_client_tags PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.contact_tags (
    id         uuid        NOT NULL,
    contact_id uuid        NOT NULL,
    tag_id     uuid        NOT NULL,
    tenant_id  uuid        NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_contact_tags PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.audit_logs (
    id          uuid         NOT NULL,
    tenant_id   uuid         NOT NULL,
    user_id     uuid,
    action      varchar(50)  NOT NULL,
    entity_type varchar(50)  NOT NULL,
    entity_id   varchar(100),
    details     varchar(2000),
    ip_address  varchar(45),
    occurred_at timestamptz  NOT NULL,
    CONSTRAINT pk_audit_logs PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.webhook_events (
    id                uuid         NOT NULL,
    phone_number_id   varchar(100) NOT NULL,
    tenant_id         uuid,
    idempotency_key   varchar(500) NOT NULL,
    status            varchar(20)  NOT NULL,
    "RawPayloadRef"   text         NOT NULL DEFAULT '',
    encrypted_payload text         NOT NULL DEFAULT '',
    signature         varchar(200),
    error_message     varchar(2000),
    retry_count       integer      NOT NULL DEFAULT 0,
    created_at        timestamptz  NOT NULL,
    processed_at      timestamptz,
    next_retry_at     timestamptz,
    CONSTRAINT pk_webhook_events PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.whatsapp_accounts (
    id               uuid         NOT NULL,
    tenant_id        uuid         NOT NULL,
    waba_id          varchar(100) NOT NULL,
    phone_number_id  varchar(100) NOT NULL,
    access_token_ref varchar(200) NOT NULL,
    is_active        boolean      NOT NULL DEFAULT false,
    created_at       timestamptz  NOT NULL,
    updated_at       timestamptz,
    version          bigint       NOT NULL DEFAULT 0,
    connection_type  varchar(20)  NOT NULL DEFAULT 'OfficialApi',
    line_number      integer      NOT NULL DEFAULT 1,
    CONSTRAINT pk_whatsapp_accounts PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.service_queues (
    id          uuid         NOT NULL,
    tenant_id   uuid         NOT NULL,
    name        varchar(100) NOT NULL,
    description varchar(500),
    color       varchar(20),
    sort_order  integer      NOT NULL DEFAULT 0,
    is_active   boolean      NOT NULL DEFAULT false,
    created_at  timestamptz  NOT NULL,
    keywords    varchar(500),
    CONSTRAINT pk_service_queues PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS whatsappai.bot_configurations (
    id                      uuid          NOT NULL,
    tenant_id               uuid          NOT NULL,
    mode                    varchar(30)   NOT NULL,
    welcome_message         varchar(1000),
    offline_message         varchar(1000),
    fallback_message        varchar(1000),
    max_tokens_per_response integer       NOT NULL DEFAULT 0,
    enabled                 boolean       NOT NULL DEFAULT false,
    created_at              timestamptz   NOT NULL,
    updated_at              timestamptz,
    version                 bigint        NOT NULL DEFAULT 0,
    handoff_message         text,
    media_message           text,
    queue_transfer_message  varchar(1000),
    CONSTRAINT pk_bot_configurations PRIMARY KEY (id)
);

-- ─── TABELAS COM FOREIGN KEY ──────────────────────────────

CREATE TABLE IF NOT EXISTS whatsappai.tenant_memberships (
    id                       uuid        NOT NULL,
    tenant_id                uuid        NOT NULL,
    user_id                  uuid        NOT NULL,
    role                     varchar(20) NOT NULL,
    status                   varchar(20) NOT NULL,
    created_at               timestamptz NOT NULL,
    deactivated_at           timestamptz,
    reactivated_at           timestamptz,
    version                  bigint      NOT NULL DEFAULT 0,
    assigned_connection_type varchar(20),
    assigned_line_number     integer,
    assigned_lines           jsonb,
    CONSTRAINT pk_tenant_memberships PRIMARY KEY (id),
    CONSTRAINT fk_tenant_memberships_tenants
        FOREIGN KEY (tenant_id) REFERENCES whatsappai.tenants(id) ON DELETE RESTRICT,
    CONSTRAINT fk_tenant_memberships_users
        FOREIGN KEY (user_id) REFERENCES whatsappai.users(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS whatsappai.invitations (
    id                 uuid         NOT NULL,
    tenant_id          uuid         NOT NULL,
    user_id            uuid,
    email              varchar(300) NOT NULL,
    token_hash         varchar(128) NOT NULL,
    purpose            varchar(20)  NOT NULL,
    status             varchar(20)  NOT NULL,
    created_by_user_id uuid         NOT NULL,
    created_at         timestamptz  NOT NULL,
    expires_at         timestamptz  NOT NULL,
    consumed_at        timestamptz,
    revoked_at         timestamptz,
    revoked_by_user_id uuid,
    version            bigint       NOT NULL DEFAULT 0,
    CONSTRAINT pk_invitations PRIMARY KEY (id),
    CONSTRAINT fk_invitations_tenants
        FOREIGN KEY (tenant_id) REFERENCES whatsappai.tenants(id) ON DELETE RESTRICT,
    CONSTRAINT fk_invitations_users
        FOREIGN KEY (user_id) REFERENCES whatsappai.users(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS whatsappai.conversations (
    id                  uuid         NOT NULL,
    tenant_id           uuid         NOT NULL,
    contact_id          uuid         NOT NULL,
    phone_number_id     varchar(100) NOT NULL,
    mode                varchar(20)  NOT NULL,
    status              varchar(20)  NOT NULL,
    assigned_to_user_id varchar(50),
    version             bigint       NOT NULL DEFAULT 1,
    created_at          timestamptz  NOT NULL,
    updated_at          timestamptz,
    last_message_at     timestamptz,
    window_expires_at   timestamptz,
    queue_id            uuid,
    CONSTRAINT pk_conversations PRIMARY KEY (id),
    CONSTRAINT fk_conversations_contacts
        FOREIGN KEY (contact_id) REFERENCES whatsappai.contacts(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS whatsappai.messages (
    id                uuid         NOT NULL,
    tenant_id         uuid         NOT NULL,
    conversation_id   uuid         NOT NULL,
    contact_id        uuid         NOT NULL,
    external_id       varchar(100),
    direction         varchar(10)  NOT NULL,
    status            varchar(20)  NOT NULL,
    type              varchar(20)  NOT NULL,
    content           varchar(4000),
    media_id          varchar(200),
    media_url         varchar(500),
    caption           varchar(4000),
    quoted_message_id varchar(100),
    idempotency_key   varchar(200),
    created_at        timestamptz  NOT NULL,
    sent_at           timestamptz,
    delivered_at      timestamptz,
    read_at           timestamptz,
    failed_at         timestamptz,
    failure_reason    varchar(2000),
    processed_by_ai   boolean      NOT NULL DEFAULT false,
    CONSTRAINT pk_messages PRIMARY KEY (id),
    CONSTRAINT fk_messages_contacts
        FOREIGN KEY (contact_id) REFERENCES whatsappai.contacts(id) ON DELETE RESTRICT,
    CONSTRAINT fk_messages_conversations
        FOREIGN KEY (conversation_id) REFERENCES whatsappai.conversations(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS whatsappai.handoff_events (
    id               uuid        NOT NULL,
    tenant_id        uuid        NOT NULL,
    conversation_id  uuid        NOT NULL,
    from_mode        varchar(20) NOT NULL,
    to_mode          varchar(20) NOT NULL,
    operator_user_id uuid,
    reason           varchar(500) NOT NULL,
    occurred_at      timestamptz  NOT NULL,
    CONSTRAINT pk_handoff_events PRIMARY KEY (id)
);

-- ─── ÍNDICES ──────────────────────────────────────────────

CREATE UNIQUE INDEX IF NOT EXISTS ix_tenants_name   ON whatsappai.tenants(name);
CREATE UNIQUE INDEX IF NOT EXISTS ix_tenants_slug   ON whatsappai.tenants(slug);
CREATE        INDEX IF NOT EXISTS ix_tenants_status ON whatsappai.tenants(status);

CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email          ON whatsappai.users(email);
CREATE        INDEX IF NOT EXISTS ix_users_security_stamp ON whatsappai.users(security_stamp);

CREATE UNIQUE INDEX IF NOT EXISTS ix_tenant_memberships_tenant_user ON whatsappai.tenant_memberships(tenant_id, user_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_tenant_memberships_user_id     ON whatsappai.tenant_memberships(user_id);
CREATE        INDEX IF NOT EXISTS ix_tenant_memberships_status      ON whatsappai.tenant_memberships(status);

CREATE UNIQUE INDEX IF NOT EXISTS ix_invitations_token_hash          ON whatsappai.invitations(token_hash);
CREATE        INDEX IF NOT EXISTS ix_invitations_tenant_email_status ON whatsappai.invitations(tenant_id, email, status);
CREATE        INDEX IF NOT EXISTS ix_invitations_expires_at          ON whatsappai.invitations(expires_at);
CREATE        INDEX IF NOT EXISTS ix_invitations_user_id             ON whatsappai.invitations(user_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_secrets_key_tenant ON whatsappai.secrets(key, tenant_id);
CREATE        INDEX IF NOT EXISTS ix_secrets_tenant_id  ON whatsappai.secrets(tenant_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_whatsapp_accounts_phone          ON whatsappai.whatsapp_accounts(phone_number_id);
CREATE        INDEX IF NOT EXISTS ix_whatsapp_accounts_tenant         ON whatsappai.whatsapp_accounts(tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_whatsapp_accounts_tenant_conn_line ON whatsappai.whatsapp_accounts(tenant_id, connection_type, line_number);

CREATE UNIQUE INDEX IF NOT EXISTS ix_webhook_events_idempotency     ON whatsappai.webhook_events(idempotency_key);
CREATE        INDEX IF NOT EXISTS ix_webhook_events_status          ON whatsappai.webhook_events(status);
CREATE        INDEX IF NOT EXISTS ix_webhook_events_next_retry      ON whatsappai.webhook_events(next_retry_at);
CREATE        INDEX IF NOT EXISTS ix_webhook_events_status_created  ON whatsappai.webhook_events(status, created_at);

CREATE UNIQUE INDEX IF NOT EXISTS ix_contacts_tenant_phone ON whatsappai.contacts(tenant_id, phone_number);
CREATE        INDEX IF NOT EXISTS ix_contacts_tenant_id    ON whatsappai.contacts(tenant_id);

CREATE        INDEX IF NOT EXISTS ix_conversations_contact              ON whatsappai.conversations(contact_id);
CREATE        INDEX IF NOT EXISTS ix_conversations_last_msg             ON whatsappai.conversations(last_message_at);
CREATE        INDEX IF NOT EXISTS ix_conversations_tenant               ON whatsappai.conversations(tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_conversations_tenant_contact_phone ON whatsappai.conversations(tenant_id, contact_id, phone_number_id);

CREATE        INDEX IF NOT EXISTS ix_messages_tenant_conv_created ON whatsappai.messages(tenant_id, conversation_id, created_at);
CREATE        INDEX IF NOT EXISTS ix_messages_external_id         ON whatsappai.messages(external_id);
CREATE        INDEX IF NOT EXISTS ix_messages_idempotency         ON whatsappai.messages(idempotency_key);
CREATE        INDEX IF NOT EXISTS ix_messages_tenant_id           ON whatsappai.messages(tenant_id);
CREATE        INDEX IF NOT EXISTS ix_messages_contact_id          ON whatsappai.messages(contact_id);
CREATE        INDEX IF NOT EXISTS ix_messages_conversation_id     ON whatsappai.messages(conversation_id);

CREATE        INDEX IF NOT EXISTS ix_handoff_tenant_conv_occurred ON whatsappai.handoff_events(tenant_id, conversation_id, occurred_at);

CREATE        INDEX IF NOT EXISTS ix_outbox_status_retry ON whatsappai.outbox_messages(status, next_retry_at);
CREATE        INDEX IF NOT EXISTS ix_outbox_tenant       ON whatsappai.outbox_messages(tenant_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_ai_creds_tenant_provider ON whatsappai.ai_provider_credentials(tenant_id, provider);

CREATE        INDEX IF NOT EXISTS ix_ai_interactions_tenant_conv ON whatsappai.ai_interactions(tenant_id, conversation_id, created_at);
CREATE        INDEX IF NOT EXISTS ix_ai_interactions_tenant      ON whatsappai.ai_interactions(tenant_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_usage_tenant_provider_metric ON whatsappai.usage_ledger(tenant_id, provider, metric, source_id);
CREATE        INDEX IF NOT EXISTS ix_usage_tenant_recorded        ON whatsappai.usage_ledger(tenant_id, recorded_at);

CREATE        INDEX IF NOT EXISTS ix_model_eval_tenant_approved ON whatsappai.model_evaluations(tenant_id, is_approved, created_at);

CREATE        INDEX IF NOT EXISTS ix_knowledge_tenant_active ON whatsappai.knowledge_items(tenant_id, is_active);
CREATE        INDEX IF NOT EXISTS ix_knowledge_tenant        ON whatsappai.knowledge_items(tenant_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_client_tags_tenant_name ON whatsappai.client_tags(tenant_id, name);

CREATE UNIQUE INDEX IF NOT EXISTS ix_contact_tags_contact_tag ON whatsappai.contact_tags(contact_id, tag_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_bot_config_tenant ON whatsappai.bot_configurations(tenant_id);

CREATE        INDEX IF NOT EXISTS ix_audit_tenant_occurred ON whatsappai.audit_logs(tenant_id, occurred_at);
CREATE        INDEX IF NOT EXISTS ix_audit_tenant_entity   ON whatsappai.audit_logs(tenant_id, entity_type, entity_id);

CREATE UNIQUE INDEX IF NOT EXISTS ix_subscription_plans_code      ON whatsappai.subscription_plans(code);
CREATE        INDEX IF NOT EXISTS ix_subscription_plans_is_active ON whatsappai.subscription_plans(is_active);

CREATE UNIQUE INDEX IF NOT EXISTS ix_service_queues_tenant_name ON whatsappai.service_queues(tenant_id, name);

-- ─── MIGRATION FALTANTE NO HISTÓRICO ─────────────────────
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260819213121_AddOperatorLineAssignment', '10.0.0')
ON CONFLICT DO NOTHING;
