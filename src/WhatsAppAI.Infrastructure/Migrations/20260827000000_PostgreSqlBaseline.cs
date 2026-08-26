using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827000000_PostgreSqlBaseline")]
public sealed class PostgreSqlBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        const string resourceName = "WhatsAppAI.Infrastructure.Migrations.PostgreSqlBaseline.sql";
        using var stream = typeof(PostgreSqlBaseline).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());

        migrationBuilder.Sql("""
            ALTER TABLE IF EXISTS whatsappai.secrets
                ALTER COLUMN encrypted_value TYPE text;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS whatsappai CASCADE;");
    }
}
