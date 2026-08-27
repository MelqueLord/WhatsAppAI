using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827160000_AddAiMessageRetry")]
public sealed class AddAiMessageRetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE whatsappai.messages
                ADD COLUMN IF NOT EXISTS ai_retry_count integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS next_ai_retry_at timestamptz NULL;
            CREATE INDEX IF NOT EXISTS "IX_messages_processed_by_ai_next_ai_retry_at"
                ON whatsappai.messages (processed_by_ai, next_ai_retry_at);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS whatsappai."IX_messages_processed_by_ai_next_ai_retry_at";
            ALTER TABLE whatsappai.messages
                DROP COLUMN IF EXISTS next_ai_retry_at,
                DROP COLUMN IF EXISTS ai_retry_count;
            """);
    }
}
