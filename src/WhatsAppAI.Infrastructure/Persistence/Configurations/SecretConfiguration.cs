using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class SecretConfiguration : IEntityTypeConfiguration<Secret>
{
    public void Configure(EntityTypeBuilder<Secret> builder)
    {
        builder.ToTable("secrets");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.Key)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.EncryptedValue)
            .HasColumnName("encrypted_value")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(s => new { s.Key, s.TenantId })
            .IsUnique();

        builder.HasIndex(s => s.TenantId);
    }
}
