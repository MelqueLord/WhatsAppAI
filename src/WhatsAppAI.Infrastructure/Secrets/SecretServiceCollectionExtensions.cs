using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Persistence.Repositories;

namespace WhatsAppAI.Infrastructure.Secrets;

public static class SecretServiceCollectionExtensions
{
    public static IServiceCollection AddSecretServices(this IServiceCollection services)
    {
        services.AddScoped<ISecretRepository, SecretRepository>();
        services.AddScoped<ISecretStore, SecretStore>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        return services;
    }
}
