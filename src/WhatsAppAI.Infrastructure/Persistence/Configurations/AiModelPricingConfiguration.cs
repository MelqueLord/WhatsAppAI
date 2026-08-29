using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AiModelPricingConfiguration : IEntityTypeConfiguration<AiModelPricing>
{
    public void Configure(EntityTypeBuilder<AiModelPricing> builder)
    {
        builder.ToTable("ai_model_pricing");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(p => p.ModelId).HasColumnName("model_id").HasMaxLength(100).IsRequired();
        builder.Property(p => p.InputCostPer1KMinorUnits).HasColumnName("input_cost_per_1k_minor_units").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(p => p.OutputCostPer1KMinorUnits).HasColumnName("output_cost_per_1k_minor_units").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(p => p.Version).HasColumnName("version").IsRequired();
        builder.Property(p => p.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(p => p.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(p => new { p.Provider, p.ModelId, p.Version }).IsUnique();
        builder.HasIndex(p => new { p.Provider, p.ModelId, p.EffectiveFrom, p.EffectiveTo });
    }
}
