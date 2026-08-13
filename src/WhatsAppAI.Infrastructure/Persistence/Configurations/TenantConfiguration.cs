using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(t => t.SuspendedAt)
            .HasColumnName("suspended_at")
            .HasColumnType("timestamptz");

        builder.Property(t => t.ReactivatedAt)
            .HasColumnName("reactivated_at")
            .HasColumnType("timestamptz");

        builder.Property(t => t.SuspensionReason)
            .HasColumnName("suspension_reason")
            .HasMaxLength(500);

        builder.Property(t => t.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .HasDefaultValue(0);

        builder.HasIndex(t => t.Name)
            .IsUnique();

        builder.HasIndex(t => t.Status);
    }
}
