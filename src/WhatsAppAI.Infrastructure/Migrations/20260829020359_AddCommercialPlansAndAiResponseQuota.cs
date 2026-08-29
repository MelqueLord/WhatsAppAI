using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialPlansAndAiResponseQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "monthly_ai_response_limit",
                schema: "whatsappai",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "automatic_distribution_enabled",
                schema: "whatsappai",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "bot_enabled",
                schema: "whatsappai",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "default_monthly_ai_response_limit",
                schema: "whatsappai",
                table: "subscription_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "default_official_api_line_count",
                schema: "whatsappai",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "default_operator_limit",
                schema: "whatsappai",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_selectable",
                schema: "whatsappai",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "tags_enabled",
                schema: "whatsappai",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE whatsappai.subscription_plans
                SET bot_enabled = TRUE,
                    tags_enabled = TRUE,
                    automatic_distribution_enabled = TRUE
                WHERE code IN ('BOT', 'IA_BOT');

                INSERT INTO whatsappai.subscription_plans
                    (id, name, code, description, ai_enabled, openai_required, ai_metrics,
                     bot_enabled, tags_enabled, automatic_distribution_enabled, is_selectable,
                     default_official_api_line_count, default_operator_limit,
                     default_monthly_ai_response_limit, max_operators, max_knowledge_items,
                     is_active, created_at)
                VALUES
                    ('8d22bb6e-d58f-40cd-88ca-11b19320de40', 'STAR', 'STAR',
                     'O essencial para começar com profissionalismo', TRUE, TRUE, TRUE,
                     FALSE, FALSE, FALSE, TRUE, 1, 2, 1500, 2, NULL, TRUE, CURRENT_TIMESTAMP),
                    ('dc0db238-aa99-4af8-b9aa-81301bfbb8f0', 'FLOW', 'FLOW',
                     'Para ganhar agilidade no atendimento', TRUE, TRUE, TRUE,
                     TRUE, TRUE, TRUE, TRUE, 2, 4, 5000, 4, NULL, TRUE, CURRENT_TIMESTAMP),
                    ('d5ef4607-81fa-4304-b116-3b264d9da7d9', 'SCALA', 'SCALA',
                     'Leve sua operação para o próximo nível', TRUE, TRUE, TRUE,
                     TRUE, TRUE, TRUE, TRUE, 3, 8, 12000, 8, NULL, TRUE, CURRENT_TIMESTAMP)
                ON CONFLICT (code) DO UPDATE SET
                    name = EXCLUDED.name,
                    description = EXCLUDED.description,
                    ai_enabled = EXCLUDED.ai_enabled,
                    openai_required = EXCLUDED.openai_required,
                    ai_metrics = EXCLUDED.ai_metrics,
                    bot_enabled = EXCLUDED.bot_enabled,
                    tags_enabled = EXCLUDED.tags_enabled,
                    automatic_distribution_enabled = EXCLUDED.automatic_distribution_enabled,
                    is_selectable = EXCLUDED.is_selectable,
                    default_official_api_line_count = EXCLUDED.default_official_api_line_count,
                    default_operator_limit = EXCLUDED.default_operator_limit,
                    default_monthly_ai_response_limit = EXCLUDED.default_monthly_ai_response_limit,
                    max_operators = EXCLUDED.max_operators,
                    is_active = EXCLUDED.is_active;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE whatsappai.tenants
                SET plan_id = legacy.id
                FROM whatsappai.subscription_plans AS legacy
                WHERE legacy.code = 'IA_BOT'
                  AND whatsappai.tenants.plan_id IN (
                      SELECT id FROM whatsappai.subscription_plans
                      WHERE code IN ('STAR', 'FLOW', 'SCALA'));

                DELETE FROM whatsappai.subscription_plans
                WHERE code IN ('STAR', 'FLOW', 'SCALA');
                """);

            migrationBuilder.DropColumn(
                name: "monthly_ai_response_limit",
                schema: "whatsappai",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "automatic_distribution_enabled",
                schema: "whatsappai",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "bot_enabled",
                schema: "whatsappai",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "default_monthly_ai_response_limit",
                schema: "whatsappai",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "default_official_api_line_count",
                schema: "whatsappai",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "default_operator_limit",
                schema: "whatsappai",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "is_selectable",
                schema: "whatsappai",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "tags_enabled",
                schema: "whatsappai",
                table: "subscription_plans");
        }
    }
}
