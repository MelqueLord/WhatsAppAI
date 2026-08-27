using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class ConsentEvidenceConfiguration : IEntityTypeConfiguration<ConsentEvidence>
{
    public void Configure(EntityTypeBuilder<ConsentEvidence> builder)
    {
        builder.ToTable("consent_evidence");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ContactId).HasColumnName("contact_id").IsRequired();
        builder.Property(x => x.ProcessingPurposeId).HasColumnName("processing_purpose_id").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(100).IsRequired();
        builder.Property(x => x.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(200);
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("datetime(6)");
        builder.Property(x => x.RecordedByUserId).HasColumnName("recorded_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.HasOne(x => x.ProcessingPurpose)
            .WithMany()
            .HasForeignKey(x => x.ProcessingPurposeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Contact>()
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.ContactId, x.ProcessingPurposeId });
        builder.HasIndex(x => x.TenantId);
    }
}
