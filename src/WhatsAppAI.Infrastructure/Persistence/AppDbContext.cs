using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    internal readonly ICurrentTenant _currentTenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<WhatsAppAccount> WhatsAppAccounts => Set<WhatsAppAccount>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<HandoffEvent> HandoffEvents => Set<HandoffEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AiProviderCredential> AiProviderCredentials => Set<AiProviderCredential>();
    public DbSet<AiInteraction> AiInteractions => Set<AiInteraction>();
    public DbSet<UsageLedger> UsageLedger => Set<UsageLedger>();
    public DbSet<ModelEvaluation> ModelEvaluations => Set<ModelEvaluation>();
    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();
    public DbSet<ClientTag> ClientTags => Set<ClientTag>();
    public DbSet<ContactTag> ContactTags => Set<ContactTag>();
    public DbSet<BotConfiguration> BotConfigurations => Set<BotConfiguration>();
    public DbSet<ServiceLine> ServiceLines => Set<ServiceLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.IsNpgsql())
        {
            modelBuilder.HasDefaultSchema("whatsappai");

            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                if (property.GetColumnType() is "datetime(6)" or "char(36)")
                    property.SetColumnType(null);
            }
        }

        var tenantId = _currentTenant.TenantId;

        modelBuilder.Entity<Contact>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Message>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<OutboxMessage>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<HandoffEvent>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<WhatsAppAccount>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<AiProviderCredential>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<AiInteraction>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<UsageLedger>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<ModelEvaluation>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<KnowledgeItem>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<ClientTag>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<ContactTag>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<BotConfiguration>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<ServiceLine>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Invitation>()
            .HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<TenantMembership>()
            .HasQueryFilter(e => e.TenantId == tenantId);

        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class TenantModelCacheKeyFactory
    : Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        return new TenantModelCacheKey((AppDbContext)context, designTime);
    }
}

internal sealed class TenantModelCacheKey(AppDbContext context, bool designTime)
    : Microsoft.EntityFrameworkCore.Infrastructure.ModelCacheKey(context, designTime)
{
    private readonly Guid? _tenantId = context._currentTenant.TenantId;

    public override bool Equals(object? obj)
    {
        return base.Equals(obj)
            && obj is TenantModelCacheKey other
            && _tenantId == other._tenantId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), _tenantId);
    }
}
