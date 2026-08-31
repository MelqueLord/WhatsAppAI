using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AiResponseQuotaReservationConfiguration
    : IEntityTypeConfiguration<AiResponseQuotaReservation>
{
    public void Configure(EntityTypeBuilder<AiResponseQuotaReservation> builder)
    {
        builder.ToTable("ai_response_quota_reservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PeriodStartUtc).HasColumnName("period_start_utc")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.SourceMessageId).HasColumnName("source_message_id").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key")
            .HasMaxLength(200).IsRequired();
        builder.Property(x => x.PackageType).HasColumnName("package_type")
            .HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.PackageReference).HasColumnName("package_reference")
            .HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CommittedAt).HasColumnName("committed_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.ReleasedAt).HasColumnName("released_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.ReleaseReason).HasColumnName("release_reason")
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PeriodStartUtc, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.SourceMessageId }).IsUnique();
    }
}
