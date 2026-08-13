using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;

namespace WhatsAppAI.Infrastructure.Meta;

public static class MetaServiceCollectionExtensions
{
    public static IServiceCollection AddMetaServices(this IServiceCollection services)
    {
        services.AddHttpClient<IWhatsAppClient, WhatsAppClient>();
        services.AddHttpClient<IMediaGateway, MediaGateway>();
        return services;
    }
}
