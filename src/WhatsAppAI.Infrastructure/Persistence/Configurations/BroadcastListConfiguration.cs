using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Broadcast;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class BroadcastListConfiguration : IEntityTypeConfiguration<BroadcastList>
{
    public void Configure(EntityTypeBuilder<BroadcastList> builder)
    {
        builder.ToTable("broadcast_lists");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(b => b.Message).HasColumnName("message").HasMaxLength(4096).IsRequired();
        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(b => b.LinePhoneNumberId).HasColumnName("line_phone_number_id").HasMaxLength(100).IsRequired();
        builder.Property(b => b.TotalCount).HasColumnName("total_count").IsRequired();
        builder.Property(b => b.SentCount).HasColumnName("sent_count").IsRequired();
        builder.Property(b => b.FailedCount).HasColumnName("failed_count").IsRequired();
        builder.Property(b => b.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(b => b.StartedAt).HasColumnName("started_at").HasColumnType("datetime(6)");
        builder.Property(b => b.FinishedAt).HasColumnName("finished_at").HasColumnType("datetime(6)");

        builder.HasIndex(b => b.TenantId);
        builder.HasIndex(b => new { b.TenantId, b.Status });
    }
}
