using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AiResponseExampleConfiguration : IEntityTypeConfiguration<AiResponseExample>
{
    public void Configure(EntityTypeBuilder<AiResponseExample> builder)
    {
        builder.ToTable("ai_response_examples");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(item => item.CustomerMessage).HasColumnName("customer_message").HasMaxLength(500).IsRequired();
        builder.Property(item => item.IdealResponse).HasColumnName("ideal_response").HasMaxLength(500).IsRequired();
        builder.Property(item => item.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.SourceInteractionId).HasColumnName("source_interaction_id");
        builder.Property(item => item.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(item => new { item.TenantId, item.IsActive });
        builder.HasIndex(item => new { item.TenantId, item.SourceInteractionId }).IsUnique();
    }
}
