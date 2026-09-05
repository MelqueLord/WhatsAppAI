using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[Migration("20260905090000_AddConsentedCustomerMemory")]
public partial class AddConsentedCustomerMemory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "customer_memories",
            schema: "whatsappai",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                consent_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                memory_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                memory_value = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customer_memories", x => x.id);
                table.ForeignKey(
                    name: "fk_customer_memories_contacts_contact_id",
                    column: x => x.contact_id,
                    principalSchema: "whatsappai",
                    principalTable: "contacts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_customer_memories_consent_evidence_consent_evidence_id",
                    column: x => x.consent_evidence_id,
                    principalSchema: "whatsappai",
                    principalTable: "consent_evidence",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_customer_memories_tenant_id_contact_id_key",
            schema: "whatsappai",
            table: "customer_memories",
            columns: new[] { "tenant_id", "contact_id", "memory_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_customer_memories_tenant_id_contact_id_is_active_expires_at",
            schema: "whatsappai",
            table: "customer_memories",
            columns: new[] { "tenant_id", "contact_id", "is_active", "expires_at" });

        migrationBuilder.CreateIndex(
            name: "ix_customer_memories_tenant_id_consent_evidence_id",
            schema: "whatsappai",
            table: "customer_memories",
            columns: new[] { "tenant_id", "consent_evidence_id" });

        migrationBuilder.CreateIndex(
            name: "IX_customer_memories_contact_id",
            schema: "whatsappai",
            table: "customer_memories",
            column: "contact_id");

        migrationBuilder.CreateIndex(
            name: "IX_customer_memories_consent_evidence_id",
            schema: "whatsappai",
            table: "customer_memories",
            column: "consent_evidence_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "customer_memories",
            schema: "whatsappai");
    }
}
