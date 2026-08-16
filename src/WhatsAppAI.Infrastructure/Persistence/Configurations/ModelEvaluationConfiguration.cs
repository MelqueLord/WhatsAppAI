using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class ModelEvaluationConfiguration : IEntityTypeConfiguration<ModelEvaluation>
{
    public void Configure(EntityTypeBuilder<ModelEvaluation> builder)
    {
        builder.ToTable("model_evaluations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.ModelId).HasColumnName("model_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EvaluatorUserId).HasColumnName("evaluator_user_id").IsRequired();
        builder.Property(e => e.QualityScore).HasColumnName("quality_score").IsRequired();
        builder.Property(e => e.HandoffRate).HasColumnName("handoff_rate").IsRequired();
        builder.Property(e => e.SafetyScore).HasColumnName("safety_score").IsRequired();
        builder.Property(e => e.CostPer1kTokens).HasColumnName("cost_per_1k_tokens").HasColumnType("decimal(10,4)").IsRequired();
        builder.Property(e => e.P95LatencyMs).HasColumnName("p95_latency_ms").IsRequired();
        builder.Property(e => e.IsApproved).HasColumnName("is_approved").IsRequired();
        builder.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(e => e.RollbackModelId).HasColumnName("rollback_model_id").HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.IsApproved, e.CreatedAt });
    }
}
