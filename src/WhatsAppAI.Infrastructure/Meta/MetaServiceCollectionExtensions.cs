using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Infrastructure.WhatsApp;

namespace WhatsAppAI.Infrastructure.Meta;

public static class MetaServiceCollectionExtensions
{
    public static IServiceCollection AddMetaServices(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var useBridge = configuration.GetValue<bool>("WhatsAppWeb:Enabled") || environment.IsDevelopment();

        if (useBridge)
        {
            var bridgeSecret = configuration["WHATSAPP_WEB_WEBHOOK_SECRET"]
                ?? configuration["WhatsAppWeb:WebhookSecret"]
                ?? throw new InvalidOperationException("WhatsAppWeb:WebhookSecret is required when the WhatsApp Web bridge is enabled.");
            services.AddHttpClient<IWhatsAppClient, WhatsAppWebClient>(client =>
                client.DefaultRequestHeaders.Add("X-WhatsApp-Web-Secret", bridgeSecret));
        }
        else
        {
            services.AddHttpClient<IWhatsAppClient, WhatsAppClient>();
        }

        services.AddHttpClient<IMediaGateway, MediaGateway>();
        return services;
    }
}
