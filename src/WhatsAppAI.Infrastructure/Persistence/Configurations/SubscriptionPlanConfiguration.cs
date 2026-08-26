using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid");

        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(p => p.AiEnabled).HasColumnName("ai_enabled").IsRequired();
        builder.Property(p => p.OpenAiRequired).HasColumnName("openai_required").IsRequired();
        builder.Property(p => p.AiMetrics).HasColumnName("ai_metrics").IsRequired();
        builder.Property(p => p.MaxOperators).HasColumnName("max_operators");
        builder.Property(p => p.MaxKnowledgeItems).HasColumnName("max_knowledge_items");
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.IsActive);
    }
}
