using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class CustomerMemoryConfiguration : IEntityTypeConfiguration<CustomerMemory>
{
    public void Configure(EntityTypeBuilder<CustomerMemory> builder)
    {
        builder.ToTable("customer_memories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ContactId).HasColumnName("contact_id").IsRequired();
        builder.Property(x => x.ConsentEvidenceId).HasColumnName("consent_evidence_id").IsRequired();
        builder.Property(x => x.Key).HasColumnName("memory_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Value).HasColumnName("memory_value").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

        builder.HasOne<Contact>()
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConsentEvidence>()
            .WithMany()
            .HasForeignKey(x => x.ConsentEvidenceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.ContactId, x.Key }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ContactId, x.IsActive, x.ExpiresAt });
        builder.HasIndex(x => new { x.TenantId, x.ConsentEvidenceId });
    }
}
