using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.ContactId)
            .HasColumnName("contact_id")
            .IsRequired();

        builder.Property(c => c.PhoneNumberId)
            .HasColumnName("phone_number_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Mode)
            .HasColumnName("mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.AssignedToUserId)
            .HasColumnName("assigned_to_user_id")
            .HasMaxLength(50);

        builder.Property(c => c.QueueId)
            .HasColumnName("queue_id");

        builder.Property(c => c.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(c => c.LastMessageAt)
            .HasColumnName("last_message_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(c => c.WindowExpiresAt)
            .HasColumnName("window_expires_at")
            .HasColumnType("timestamp with time zone");

        builder.HasOne(c => c.Contact)
            .WithMany(c => c.Conversations)
            .HasForeignKey(c => c.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TenantId, c.ContactId, c.PhoneNumberId })
            .IsUnique();

        builder.HasIndex(c => c.TenantId);

        builder.HasIndex(c => c.LastMessageAt);
    }
}
