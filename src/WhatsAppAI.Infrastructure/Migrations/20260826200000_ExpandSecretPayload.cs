using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260826200000_ExpandSecretPayload")]
public sealed class ExpandSecretPayload : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql("""
                ALTER TABLE "whatsappai"."secrets"
                    ALTER COLUMN "encrypted_value" TYPE text;
                """);
            return;
        }

        if (ActiveProvider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            migrationBuilder.Sql("""
                ALTER TABLE `secrets`
                    MODIFY COLUMN `encrypted_value` LONGTEXT NOT NULL;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql("""
                ALTER TABLE "whatsappai"."secrets"
                    ALTER COLUMN "encrypted_value" TYPE varchar(2000);
                """);
            return;
        }

        if (ActiveProvider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            migrationBuilder.Sql("""
                ALTER TABLE `secrets`
                    MODIFY COLUMN `encrypted_value` varchar(2000) NOT NULL;
                """);
        }
    }
}
