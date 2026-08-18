using Microsoft.Extensions.DependencyInjection;

namespace WhatsAppAI.Infrastructure;

/// <summary>
/// Deprecated: use <see cref="AiProviderServiceCollectionExtensions.AddAiProviderServices"/> instead.
/// This method is kept for backward compatibility and delegates to the new unified registration.
/// </summary>
public static class OpenAIServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiServices(this IServiceCollection services)
    {
        return services.AddAiProviderServices();
    }
}
