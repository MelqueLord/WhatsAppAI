using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(m => m.DeactivatedAt)
            .HasColumnName("deactivated_at")
            .HasColumnType("datetime(6)");

        builder.Property(m => m.ReactivatedAt)
            .HasColumnName("reactivated_at")
            .HasColumnType("datetime(6)");

        builder.Property(m => m.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .HasDefaultValue(0);

        builder.Property(m => m.AssignedConnectionType)
            .HasColumnName("assigned_connection_type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.AssignedLineNumber)
            .HasColumnName("assigned_line_number");

        builder.Ignore(m => m.AssignedLinesJson);
        builder.Ignore(m => m.AssignedLines);

        builder.HasOne(m => m.Tenant)
            .WithMany(t => t.Memberships)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.TenantId, m.UserId })
            .IsUnique();

        builder.HasIndex(m => m.UserId)
            .IsUnique();

        builder.HasIndex(m => m.Status);
    }
}
