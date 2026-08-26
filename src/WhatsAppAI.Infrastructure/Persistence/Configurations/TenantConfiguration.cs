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

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.PlanId)
            .HasColumnName("plan_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.OfficialApiLineCount)
            .HasColumnName("official_api_line_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(t => t.QrCodeLineCount)
            .HasColumnName("qr_code_line_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(t => t.OperatorLimit)
            .HasColumnName("operator_limit")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.DueDate)
            .HasColumnName("due_date")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.LastPaymentAt)
            .HasColumnName("last_payment_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.ActivatedAt)
            .HasColumnName("activated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.SuspendedAt)
            .HasColumnName("suspended_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.ReactivatedAt)
            .HasColumnName("reactivated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.ClosedAt)
            .HasColumnName("closed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.SuspensionReason)
            .HasColumnName("suspension_reason")
            .HasMaxLength(500);

        builder.Property(t => t.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .HasDefaultValue(0);

        builder.HasIndex(t => t.Name)
            .IsUnique();

        builder.HasIndex(t => t.Slug)
            .IsUnique();

        builder.HasIndex(t => t.Status);
    }
}
