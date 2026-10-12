using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Onboarding;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AiOnboardingFormConfiguration : IEntityTypeConfiguration<AiOnboardingForm>
{
    public void Configure(EntityTypeBuilder<AiOnboardingForm> builder)
    {
        builder.ToTable("ai_onboarding_forms");
        builder.HasKey(form => form.Id);

        builder.Property(form => form.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(form => form.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(form => form.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(form => form.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(form => form.CompanyName).HasColumnName("company_name").HasMaxLength(200);
        builder.Property(form => form.AnswersJson).HasColumnName("answers_json").HasColumnType("jsonb");
        builder.Property(form => form.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(form => form.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(form => form.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(form => form.SubmittedAt).HasColumnName("submitted_at").HasColumnType("timestamp with time zone");
        builder.Property(form => form.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        builder.Property(form => form.Version).HasColumnName("version").IsConcurrencyToken().HasDefaultValue(0);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(form => form.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(form => form.TokenHash).IsUnique();
        builder.HasIndex(form => new { form.TenantId, form.Status, form.CreatedAt });
        builder.HasIndex(form => form.ExpiresAt);
    }
}
