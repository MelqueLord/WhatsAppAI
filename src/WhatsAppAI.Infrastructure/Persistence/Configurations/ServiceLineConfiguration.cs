using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class ServiceLineConfiguration : IEntityTypeConfiguration<ServiceLine>
{
    public void Configure(EntityTypeBuilder<ServiceLine> builder)
    {
        builder.ToTable("service_queues");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(q => q.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(q => q.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(q => q.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(q => q.Color).HasColumnName("color").HasMaxLength(20);
        builder.Property(q => q.Keywords).HasColumnName("keywords").HasMaxLength(500);
        builder.Property(q => q.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(q => q.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(q => q.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(q => new { q.TenantId, q.Name }).IsUnique();
    }
}
