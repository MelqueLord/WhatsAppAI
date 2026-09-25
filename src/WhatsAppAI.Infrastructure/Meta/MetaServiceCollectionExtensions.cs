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
            var serviceToken = configuration["WhatsAppWeb:ServiceToken"]
                ?? throw new InvalidOperationException("WhatsAppWeb:ServiceToken is required when the WhatsApp Web bridge is enabled.");
            var serviceId = configuration["WhatsAppWeb:ApiServiceId"] ?? "webapi";
            qrClient.ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Id", serviceId);
                client.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Token", serviceToken);
            });
        }

        services.AddScoped<IWhatsAppClientResolver, WhatsAppClientResolver>();

        services.AddHttpClient<IMediaGateway, MediaGateway>();
        return services;
    }
}
