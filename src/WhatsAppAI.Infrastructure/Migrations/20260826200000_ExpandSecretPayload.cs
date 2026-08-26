using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
public sealed class ExpandSecretPayload : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "whatsappai"."secrets"
                ALTER COLUMN "encrypted_value" TYPE text;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "whatsappai"."secrets"
                ALTER COLUMN "encrypted_value" TYPE varchar(2000);
            """);
    }
}
