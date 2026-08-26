using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class ClientTagConfiguration : IEntityTypeConfiguration<ClientTag>
{
    public void Configure(EntityTypeBuilder<ClientTag> builder)
    {
        builder.ToTable("client_tags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(t => t.Color).HasColumnName("color").HasMaxLength(20);
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
    }
}
