using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Anthropic;
using WhatsAppAI.Infrastructure.Gemini;
using WhatsAppAI.Infrastructure.OpenAI;
using WhatsAppAI.Infrastructure.Xiaomi;

namespace WhatsAppAI.Infrastructure;

public static class AiProviderServiceCollectionExtensions
{
    public static IServiceCollection AddAiProviderServices(this IServiceCollection services)
    {
        // Register individual providers as named HttpClients
        services.AddHttpClient("openai", client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient("gemini", client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient("anthropic", client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient("xiaomi", client =>
        {
            client.BaseAddress = new Uri("https://api.xiaomi.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Register OpenAI provider
        services.AddScoped<OpenAiProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenAiProvider>>();
            return new OpenAiProvider(factory.CreateClient("openai"), logger);
        });

        // Register Gemini provider
        services.AddScoped<GeminiProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GeminiProvider>>();
            return new GeminiProvider(factory.CreateClient("gemini"), logger);
        });

        // Register Anthropic provider
        services.AddScoped<AnthropicProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AnthropicProvider>>();
            return new AnthropicProvider(factory.CreateClient("anthropic"), logger);
        });

        // Register Xiaomi provider
        services.AddScoped<XiaomiProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<XiaomiProvider>>();
            return new XiaomiProvider(factory.CreateClient("xiaomi"), logger);
        });

        // Register the resolver with all providers
        services.AddScoped<IAiProviderResolver>(sp =>
        {
            var openai = sp.GetRequiredService<OpenAiProvider>();
            var gemini = sp.GetRequiredService<GeminiProvider>();
            var anthropic = sp.GetRequiredService<AnthropicProvider>();
            var xiaomi = sp.GetRequiredService<XiaomiProvider>();

            var providers = new List<KeyValuePair<string, IAiProvider>>
            {
                new("openai", openai),
                new("gemini", gemini),
                new("anthropic", anthropic),
                new("xiaomi", xiaomi)
            };

            return new AiProviderResolver(providers);
        });

        // Backward compatibility: register IAiProvider as OpenAI (default)
        // This allows existing code that injects IAiProvider directly to continue working.
        // New code should use IAiProviderResolver instead.
        services.AddScoped<IAiProvider>(sp =>
            sp.GetRequiredService<IAiProviderResolver>().Resolve("openai"));

        return services;
    }
}
