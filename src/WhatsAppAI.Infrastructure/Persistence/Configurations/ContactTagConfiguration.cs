using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class ContactTagConfiguration : IEntityTypeConfiguration<ContactTag>
{
    public void Configure(EntityTypeBuilder<ContactTag> builder)
    {
        builder.ToTable("contact_tags");
        builder.HasKey(ct => ct.Id);
        builder.Property(ct => ct.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(ct => ct.ContactId).HasColumnName("contact_id").IsRequired();
        builder.Property(ct => ct.TagId).HasColumnName("tag_id").IsRequired();
        builder.Property(ct => ct.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(ct => ct.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(ct => new { ct.ContactId, ct.TagId }).IsUnique();
    }
}
