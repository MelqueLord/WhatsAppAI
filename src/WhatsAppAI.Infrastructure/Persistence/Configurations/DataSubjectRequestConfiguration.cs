using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
    {
        builder.ToTable("data_subject_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ContactId).HasColumnName("contact_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.DueAt).HasColumnName("due_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("datetime(6)");
        builder.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(500);
        builder.Property(x => x.ReviewAt).HasColumnName("review_at").HasColumnType("datetime(6)");
        builder.HasOne<Contact>()
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.DueAt });
        builder.HasIndex(x => x.TenantId);
    }
}
