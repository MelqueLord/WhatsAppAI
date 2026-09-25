using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class InboundMediaAttachmentConfiguration : IEntityTypeConfiguration<InboundMediaAttachment>
{
    public void Configure(EntityTypeBuilder<InboundMediaAttachment> builder)
    {
        builder.ToTable("inbound_media_attachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ExternalMessageId).HasColumnName("external_message_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Length).HasColumnName("length").IsRequired();
        builder.Property(x => x.EncryptedContent).HasColumnName("encrypted_content").HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ExternalMessageId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
    }
}
