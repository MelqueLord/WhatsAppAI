using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827133343_AddPrivacyControls")]
public sealed class AddPrivacyControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS whatsappai.processing_purposes (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                name varchar(100) NOT NULL,
                description varchar(500) NOT NULL,
                legal_basis varchar(40) NOT NULL,
                retention_days integer NOT NULL,
                is_active boolean NOT NULL,
                created_by_user_id uuid NOT NULL,
                created_at timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_processing_purposes_tenant_id_name"
                ON whatsappai.processing_purposes (tenant_id, name);

            CREATE TABLE IF NOT EXISTS whatsappai.data_subject_requests (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                contact_id uuid NOT NULL REFERENCES whatsappai.contacts(id) ON DELETE RESTRICT,
                type varchar(30) NOT NULL,
                status varchar(20) NOT NULL,
                requested_by_user_id uuid NOT NULL,
                requested_at timestamptz NOT NULL,
                due_at timestamptz NOT NULL,
                resolved_by_user_id uuid NULL,
                resolved_at timestamptz NULL,
                decision_reason varchar(500) NULL,
                review_at timestamptz NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_data_subject_requests_tenant_id_status_due_at"
                ON whatsappai.data_subject_requests (tenant_id, status, due_at);

            CREATE TABLE IF NOT EXISTS whatsappai.consent_evidence (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                contact_id uuid NOT NULL REFERENCES whatsappai.contacts(id) ON DELETE RESTRICT,
                processing_purpose_id uuid NOT NULL REFERENCES whatsappai.processing_purposes(id) ON DELETE RESTRICT,
                source varchar(100) NOT NULL,
                evidence_reference varchar(200) NULL,
                granted_at timestamptz NOT NULL,
                revoked_at timestamptz NULL,
                recorded_by_user_id uuid NOT NULL,
                created_at timestamptz NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_consent_evidence_tenant_id_contact_id_processing_purpose_id"
                ON whatsappai.consent_evidence (tenant_id, contact_id, processing_purpose_id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS whatsappai.consent_evidence;
            DROP TABLE IF EXISTS whatsappai.data_subject_requests;
            DROP TABLE IF EXISTS whatsappai.processing_purposes;
            """);
    }
}
