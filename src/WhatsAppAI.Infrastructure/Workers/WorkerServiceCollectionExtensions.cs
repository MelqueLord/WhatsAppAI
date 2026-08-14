using Microsoft.Extensions.DependencyInjection;

namespace WhatsAppAI.Infrastructure.Workers;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddWorkers(this IServiceCollection services)
    {
        services.AddHostedService<WebhookProcessingWorker>();
        services.AddHostedService<OutboxProcessingWorker>();
        services.AddHostedService<AiOrchestrationWorker>();
        services.AddHostedService<RetentionWorker>();
        return services;
    }
}
