using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class UsageLedgerConfiguration : IEntityTypeConfiguration<UsageLedger>
{
    public void Configure(EntityTypeBuilder<UsageLedger> builder)
    {
        builder.ToTable("usage_ledger");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(u => u.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(u => u.Metric).HasColumnName("metric").HasMaxLength(50).IsRequired();
        builder.Property(u => u.SourceId).HasColumnName("source_id").HasMaxLength(200).IsRequired();
        builder.Property(u => u.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(u => u.Unit).HasColumnName("unit").HasMaxLength(20);
        builder.Property(u => u.CostMinorUnits).HasColumnName("cost_minor_units");
        builder.Property(u => u.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(u => u.RecordedAt).HasColumnName("recorded_at").HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(u => new { u.TenantId, u.Provider, u.Metric, u.SourceId }).IsUnique();
        builder.HasIndex(u => new { u.TenantId, u.RecordedAt });
    }
}
