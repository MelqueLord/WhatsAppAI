using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class BotConfigurationConfiguration : IEntityTypeConfiguration<BotConfiguration>
{
    public void Configure(EntityTypeBuilder<BotConfiguration> builder)
    {
        builder.ToTable("bot_configurations");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired().IsConcurrencyToken(false);
        builder.Property(b => b.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(b => b.WelcomeMessage).HasColumnName("welcome_message").HasMaxLength(1000);
        builder.Property(b => b.ReturningMessage).HasColumnName("returning_message").HasMaxLength(1000);
        builder.Property(b => b.FlowStepsJson).HasColumnName("flow_steps_json").HasMaxLength(20000);
        builder.Property(b => b.OfflineMessage).HasColumnName("offline_message").HasMaxLength(1000);
        builder.Property(b => b.FallbackMessage).HasColumnName("fallback_message").HasMaxLength(1000);
        builder.Property(b => b.HandoffMessage).HasColumnName("handoff_message").HasMaxLength(1000);
        builder.Property(b => b.QueueTransferMessage).HasColumnName("queue_transfer_message").HasMaxLength(1000);
        builder.Property(b => b.MediaMessage).HasColumnName("media_message").HasMaxLength(1000);
        builder.Property(b => b.BusinessHoursEnabled).HasColumnName("business_hours_enabled").IsRequired();
        builder.Property(b => b.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100).HasDefaultValue("America/Sao_Paulo").IsRequired();
        builder.Property(b => b.BusinessHoursJson).HasColumnName("business_hours_json").HasColumnType("text");
        builder.Property(b => b.ConfidenceThreshold).HasColumnName("confidence_threshold").HasDefaultValue(0.5).IsRequired();
        builder.Property(b => b.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(b => b.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasIndex(b => b.TenantId).IsUnique();
    }
}
