using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Infrastructure.WhatsApp;

namespace WhatsAppAI.Infrastructure.Meta;

public static class MetaServiceCollectionExtensions
{
    public static IServiceCollection AddMetaServices(this IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // Use WhatsApp Web client for development (QR code connection)
            services.AddSingleton<IWhatsAppClient, WhatsAppWebClient>();
        }
        else
        {
            // Use official WhatsApp Cloud API for production
            services.AddHttpClient<IWhatsAppClient, WhatsAppClient>();
        }

        services.AddHttpClient<IMediaGateway, MediaGateway>();
        return services;
    }
}
