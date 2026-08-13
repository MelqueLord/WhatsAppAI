using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppAccountConfiguration : IEntityTypeConfiguration<WhatsAppAccount>
{
    public void Configure(EntityTypeBuilder<WhatsAppAccount> builder)
    {
        builder.ToTable("whatsapp_accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(a => a.WabaId)
            .HasColumnName("waba_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.PhoneNumberId)
            .HasColumnName("phone_number_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.AccessTokenRef)
            .HasColumnName("access_token_ref")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        builder.Property(a => a.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .HasDefaultValue(0);

        builder.HasIndex(a => a.TenantId)
            .IsUnique()
            .HasFilter("is_active = true");

        builder.HasIndex(a => a.PhoneNumberId)
            .IsUnique();
    }
}
