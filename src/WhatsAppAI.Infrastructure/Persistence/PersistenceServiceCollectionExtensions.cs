using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Application.Messaging;
using WhatsAppAI.Infrastructure.Conversations;
using WhatsAppAI.Infrastructure.Persistence.Repositories;

namespace WhatsAppAI.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString,
        string provider = "PostgreSQL")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.ToUpperInvariant())
            {
                case "SQLITE":
                    options.UseSqlite(connectionString);
                    break;
                case "POSTGRESQL":
                default:
                    options.UseNpgsql(connectionString);
                    break;
            }
        });

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<ISecretRepository, SecretRepository>();
        services.AddScoped<IWhatsAppAccountRepository, WhatsAppAccountRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationQueries, ConversationQueries>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }

    // Keep backward compatibility
    public static IServiceCollection AddPostgreSqlPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddPersistence(connectionString, "PostgreSQL");
    }
}
