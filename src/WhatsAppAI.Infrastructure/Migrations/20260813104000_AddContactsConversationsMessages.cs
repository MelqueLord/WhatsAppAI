using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddContactsConversationsMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "contacts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                profile_picture_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_message_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
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
                phone_number_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                assigned_to_user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                version = table.Column<uint>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_message_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                window_expires_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
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
                external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                media_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                media_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                caption = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                quoted_message_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                sent_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                delivered_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                read_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                failed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
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

        migrationBuilder.CreateIndex(
            name: "ix_contacts_phone_number",
            table: "contacts",
            columns: new[] { "tenant_id", "phone_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_contacts_tenant_id",
            table: "contacts",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_conversations_contact_id",
            table: "conversations",
            column: "contact_id");

        migrationBuilder.CreateIndex(
            name: "ix_conversations_last_message_at",
            table: "conversations",
            column: "last_message_at");

        migrationBuilder.CreateIndex(
            name: "ix_conversations_tenant_id",
            table: "conversations",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ix_conversations_tenant_id_contact_id_phone_number_id",
            table: "conversations",
            columns: new[] { "tenant_id", "contact_id", "phone_number_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_messages_conversation_id",
            table: "messages",
            columns: new[] { "tenant_id", "conversation_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_messages_external_id",
            table: "messages",
            column: "external_id",
            unique: true,
            filter: "external_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_messages_idempotency_key",
            table: "messages",
            column: "idempotency_key",
            unique: true,
            filter: "idempotency_key IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_messages_tenant_id",
            table: "messages",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "messages");

        migrationBuilder.DropTable(
            name: "conversations");

        migrationBuilder.DropTable(
            name: "contacts");
    }
}
