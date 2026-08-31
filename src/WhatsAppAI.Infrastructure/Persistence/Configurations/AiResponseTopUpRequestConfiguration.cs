using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AiResponseTopUpRequestConfiguration
    : IEntityTypeConfiguration<AiResponseTopUpRequest>
{
    public void Configure(EntityTypeBuilder<AiResponseTopUpRequest> builder)
    {
        builder.ToTable("ai_response_top_up_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PeriodStartUtc).HasColumnName("period_start_utc")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key")
            .HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason")
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PeriodStartUtc, x.Status });
    }
}
