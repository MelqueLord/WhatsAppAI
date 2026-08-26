using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeItemConfiguration : IEntityTypeConfiguration<KnowledgeItem>
{
    public void Configure(EntityTypeBuilder<KnowledgeItem> builder)
    {
        builder.ToTable("knowledge_items");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(k => k.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(k => k.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(k => k.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
        builder.Property(k => k.Priority).HasColumnName("priority").IsRequired();
        builder.Property(k => k.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(k => k.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(k => k.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(k => k.DeactivatedAt).HasColumnName("deactivated_at").HasColumnType("timestamp with time zone");
        builder.Property(k => k.ReactivatedAt).HasColumnName("reactivated_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(k => new { k.TenantId, k.IsActive });
        builder.HasIndex(k => k.TenantId);
    }
}
