using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class OutboundMediaAttachmentConfiguration : IEntityTypeConfiguration<OutboundMediaAttachment>
{
    public void Configure(EntityTypeBuilder<OutboundMediaAttachment> builder)
    {
        builder.ToTable("outbound_media_attachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Length).HasColumnName("length").IsRequired();
        builder.Property(x => x.EncryptedContent).HasColumnName("encrypted_content").HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.PurgedAt).HasColumnName("purged_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.TenantId, x.MessageId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasOne<Message>().WithOne().HasForeignKey<OutboundMediaAttachment>(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
