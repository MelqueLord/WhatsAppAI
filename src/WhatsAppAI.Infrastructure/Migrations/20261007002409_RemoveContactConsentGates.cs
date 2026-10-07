using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContactConsentGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customer_memories_consent_evidence_consent_evidence_id",
                schema: "whatsappai",
                table: "customer_memories");

            migrationBuilder.DropIndex(
                name: "IX_customer_memories_consent_evidence_id",
                schema: "whatsappai",
                table: "customer_memories");

            migrationBuilder.DropIndex(
                name: "IX_customer_memories_tenant_id_consent_evidence_id",
                schema: "whatsappai",
                table: "customer_memories");

            migrationBuilder.AlterColumn<Guid>(
                name: "consent_evidence_id",
                schema: "whatsappai",
                table: "customer_memories",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.Sql("""
                UPDATE whatsappai.outbox_messages AS outbox
                SET status = 'Dead', processed_at = CURRENT_TIMESTAMP, last_error = 'Consent notice retired'
                FROM whatsappai.messages AS message
                WHERE outbox.message_id = message.id
                  AND (message.idempotency_key LIKE 'consent-request:%'
                    OR message.idempotency_key LIKE 'consent-confirmation:%')
                  AND outbox.status IN ('Pending', 'Processing');

                UPDATE whatsappai.messages
                SET status = 'Failed', failed_at = CURRENT_TIMESTAMP, failure_reason = 'Consent notice retired'
                WHERE (idempotency_key LIKE 'consent-request:%'
                    OR idempotency_key LIKE 'consent-confirmation:%')
                  AND status = 'Queued';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE whatsappai.messages
                SET status = 'Queued', failed_at = NULL, failure_reason = NULL
                WHERE (idempotency_key LIKE 'consent-request:%'
                    OR idempotency_key LIKE 'consent-confirmation:%')
                  AND status = 'Failed' AND failure_reason = 'Consent notice retired';

                UPDATE whatsappai.outbox_messages
                SET status = 'Pending', processed_at = NULL, last_error = NULL
                WHERE status = 'Dead' AND last_error = 'Consent notice retired';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_customer_memories_consent_evidence_id",
                schema: "whatsappai",
                table: "customer_memories",
                column: "consent_evidence_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_memories_tenant_id_consent_evidence_id",
                schema: "whatsappai",
                table: "customer_memories",
                columns: new[] { "tenant_id", "consent_evidence_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_customer_memories_consent_evidence_consent_evidence_id",
                schema: "whatsappai",
                table: "customer_memories",
                column: "consent_evidence_id",
                principalSchema: "whatsappai",
                principalTable: "consent_evidence",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
