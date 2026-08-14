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
        builder.Property(b => b.OfflineMessage).HasColumnName("offline_message").HasMaxLength(1000);
        builder.Property(b => b.FallbackMessage).HasColumnName("fallback_message").HasMaxLength(1000);
        builder.Property(b => b.MaxTokensPerResponse).HasColumnName("max_tokens_per_response").IsRequired();
        builder.Property(b => b.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        builder.Property(b => b.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.HasIndex(b => b.TenantId).IsUnique();
    }
}
