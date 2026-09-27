using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppBusinessAccountConfiguration : IEntityTypeConfiguration<WhatsAppBusinessAccount>
{
    public void Configure(EntityTypeBuilder<WhatsAppBusinessAccount> builder)
    {
        builder.ToTable("whatsapp_business_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.WabaId).HasColumnName("waba_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().HasDefaultValue(0u);
        builder.HasIndex(x => x.WabaId).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Id }).IsUnique();
    }
}
