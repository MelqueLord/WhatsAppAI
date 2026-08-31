using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "whatsappai",
                table: "knowledge_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_items_tenant_id_category_is_active",
                schema: "whatsappai",
                table: "knowledge_items",
                columns: new[] { "tenant_id", "category", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_knowledge_items_tenant_id_category_is_active",
                schema: "whatsappai",
                table: "knowledge_items");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "whatsappai",
                table: "knowledge_items");
        }
    }
}
