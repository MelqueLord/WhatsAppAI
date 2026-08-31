using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AiProviderCredentialConfiguration : IEntityTypeConfiguration<AiProviderCredential>
{
    public void Configure(EntityTypeBuilder<AiProviderCredential> builder)
    {
        builder.ToTable("ai_provider_credentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.ApiKeyRef)
            .HasColumnName("api_key_ref")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.CredentialScope)
            .HasColumnName("credential_scope")
            .HasMaxLength(30)
            .HasDefaultValue(AiCredentialScopes.TenantProject)
            .IsRequired();

        builder.Property(c => c.SystemPrompt)
            .HasColumnName("system_prompt")
            .HasMaxLength(4000);

        builder.Property(c => c.RoutingQueueIdsJson)
            .HasColumnName("routing_queue_ids_json")
            .HasColumnType("TEXT");

        builder.Property(c => c.RoutingTagIdsJson)
            .HasColumnName("routing_tag_ids_json")
            .HasColumnType("TEXT");

        builder.Property(c => c.MaxTokensPerResponse)
            .HasColumnName("max_tokens_per_response")
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(c => c.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.Provider })
            .IsUnique();
    }
}
