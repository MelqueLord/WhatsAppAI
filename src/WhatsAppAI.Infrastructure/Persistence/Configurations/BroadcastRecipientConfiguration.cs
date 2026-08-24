using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Broadcast;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class BroadcastRecipientConfiguration : IEntityTypeConfiguration<BroadcastRecipient>
{
    public void Configure(EntityTypeBuilder<BroadcastRecipient> builder)
    {
        builder.ToTable("broadcast_recipients");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.BroadcastListId).HasColumnName("broadcast_list_id").IsRequired();
        builder.Property(r => r.ContactId).HasColumnName("contact_id").IsRequired();
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(r => r.SentAt).HasColumnName("sent_at").HasColumnType("datetime(6)");

        builder.HasOne(r => r.BroadcastList)
            .WithMany()
            .HasForeignKey(r => r.BroadcastListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.BroadcastListId);
        builder.HasIndex(r => new { r.BroadcastListId, r.Status });
        builder.HasIndex(r => new { r.TenantId, r.BroadcastListId, r.ContactId }).IsUnique();
    }
}
