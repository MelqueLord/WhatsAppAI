using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppTemplateSubmissionConfiguration : IEntityTypeConfiguration<WhatsAppTemplateSubmission>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplateSubmission> builder)
    {
        builder.ToTable("whatsapp_template_submissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.WhatsAppBusinessAccountId).HasColumnName("whatsapp_business_account_id").IsRequired();
        builder.Property(x => x.WhatsAppMessageTemplateId).HasColumnName("whatsapp_message_template_id").IsRequired();
        builder.Property(x => x.SourceWhatsAppAccountId).HasColumnName("source_whatsapp_account_id").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RequestFingerprint).HasColumnName("request_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ClaimedAt).HasColumnName("claimed_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ClaimExpiresAt).HasColumnName("claim_expires_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);
        builder.Property(x => x.LastErrorCategory).HasColumnName("last_error_category").HasMaxLength(60);
        builder.Property(x => x.LastErrorMessage).HasColumnName("last_error_message").HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AcceptedAt).HasColumnName("accepted_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().HasDefaultValue(0u);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.HasOne<WhatsAppBusinessAccount>().WithMany().HasForeignKey(x => x.WhatsAppBusinessAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WhatsAppMessageTemplate>().WithMany().HasForeignKey(x => x.WhatsAppMessageTemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WhatsAppAccount>().WithMany().HasForeignKey(x => x.SourceWhatsAppAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
