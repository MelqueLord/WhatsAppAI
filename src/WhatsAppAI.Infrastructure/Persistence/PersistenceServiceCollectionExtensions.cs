using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Administration;
using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Application.Audit;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Application.Contacts;
using WhatsAppAI.Application.Messaging;
using WhatsAppAI.Infrastructure.Contacts;
using WhatsAppAI.Infrastructure.Administration;
using WhatsAppAI.Infrastructure.Conversations;
using WhatsAppAI.Infrastructure.Persistence.Repositories;

namespace WhatsAppAI.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var pooledConnectionString = LimitConnectionPool(connectionString);

        services.AddSingleton<TenantSaveChangesInterceptor>();
        services.AddSingleton<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
            options.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

            options.UseNpgsql(pooledConnectionString);
        });

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IInfrastructureCapacityReader, InfrastructureCapacityReader>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<ISecretRepository, SecretRepository>();
        services.AddScoped<IWhatsAppAccountRepository, WhatsAppAccountRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IContactImportFileReader, ContactImportFileReader>();
        services.AddScoped<ContactImportService>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IHandoffEventRepository, HandoffEventRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IAiProviderCredentialRepository, AiProviderCredentialRepository>();
        services.AddScoped<IKnowledgeItemRepository, KnowledgeItemRepository>();
        services.AddScoped<IClientTagRepository, ClientTagRepository>();
        services.AddScoped<IContactTagRepository, ContactTagRepository>();
        services.AddScoped<IBotConfigurationRepository, BotConfigurationRepository>();
        services.AddScoped<IServiceLineRepository, ServiceLineRepository>();
        services.AddScoped<IAiInteractionRepository, AiInteractionRepository>();
        services.AddScoped<IUsageLedgerRepository, UsageLedgerRepository>();
        services.AddScoped<IModelEvaluationRepository, ModelEvaluationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBroadcastRepository, BroadcastRepository>();
        services.AddScoped<AuditService>();
        services.AddScoped<IConversationQueries, ConversationQueries>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ContextAssembler>();

        return services;
    }

    internal static string LimitConnectionPool(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Pooling = true;
        builder.MaxPoolSize = Math.Min(builder.MaxPoolSize, 10);

        return builder.ConnectionString;
    }
}
