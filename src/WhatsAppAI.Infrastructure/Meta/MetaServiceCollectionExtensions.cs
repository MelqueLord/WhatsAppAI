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

        services.AddHttpClient<WhatsAppClient>();
        var qrClient = services.AddHttpClient<WhatsAppWebClient>();

        if (useBridge)
        {
            var bridgeSecret = configuration["WHATSAPP_WEB_WEBHOOK_SECRET"]
                ?? configuration["WhatsAppWeb:WebhookSecret"]
                ?? throw new InvalidOperationException("WhatsAppWeb:WebhookSecret is required when the WhatsApp Web bridge is enabled.");
            qrClient.ConfigureHttpClient(client =>
                client.DefaultRequestHeaders.Add("X-WhatsApp-Web-Secret", bridgeSecret));
        }

        services.AddScoped<IWhatsAppClientResolver, WhatsAppClientResolver>();

        services.AddHttpClient<IMediaGateway, MediaGateway>();
        return services;
    }
}
