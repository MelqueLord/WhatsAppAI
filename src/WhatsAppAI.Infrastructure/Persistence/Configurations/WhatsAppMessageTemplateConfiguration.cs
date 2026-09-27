using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppMessageTemplateConfiguration : IEntityTypeConfiguration<WhatsAppMessageTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessageTemplate> builder)
    {
        builder.ToTable("whatsapp_message_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.WhatsAppBusinessAccountId).HasColumnName("whatsapp_business_account_id").IsRequired();
        builder.Property(x => x.MetaTemplateId).HasColumnName("meta_template_id").HasMaxLength(100);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
        builder.Property(x => x.Language).HasColumnName("language").HasMaxLength(20).IsRequired();
        builder.Property(x => x.RequestedCategory).HasColumnName("requested_category").HasMaxLength(40);
        builder.Property(x => x.EffectiveCategory).HasColumnName("effective_category").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ParameterFormat).HasColumnName("parameter_format").HasMaxLength(20).IsRequired();
        builder.Property(x => x.BodyText).HasColumnName("body_text").HasMaxLength(1024).IsRequired();
        builder.Property(x => x.FooterText).HasColumnName("footer_text").HasMaxLength(60);
        builder.Property(x => x.BodyExamplesJson).HasColumnName("body_examples_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.BodyParameterCount).HasColumnName("body_parameter_count").IsRequired();
        builder.Property(x => x.ComponentsJson).HasColumnName("components_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ReviewStatus).HasColumnName("review_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProviderRawStatus).HasColumnName("provider_raw_status").HasMaxLength(60).IsRequired();
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(x => x.Recommendation).HasColumnName("recommendation").HasMaxLength(1000);
        builder.Property(x => x.IsInboxCompatible).HasColumnName("is_inbox_compatible").IsRequired();
        builder.Property(x => x.IsBroadcastCompatible).HasColumnName("is_broadcast_compatible").IsRequired();
        builder.Property(x => x.ProviderUpdatedAt).HasColumnName("provider_updated_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().HasDefaultValue(0u);
        builder.HasIndex(x => new { x.TenantId, x.WhatsAppBusinessAccountId, x.Name, x.Language }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.WhatsAppBusinessAccountId, x.MetaTemplateId }).IsUnique().HasFilter("meta_template_id IS NOT NULL");
        builder.HasOne<WhatsAppBusinessAccount>().WithMany().HasForeignKey(x => x.WhatsAppBusinessAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
