using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.OpenAI;

namespace WhatsAppAI.Infrastructure;

public static class OpenAIServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiServices(this IServiceCollection services)
    {
        services.AddHttpClient<IAiProvider, OpenAiProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        return services;
    }
}
