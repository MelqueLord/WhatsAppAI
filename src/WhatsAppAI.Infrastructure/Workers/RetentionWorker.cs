using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class RetentionWorker(
    IServiceProvider serviceProvider,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Retention Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in retention worker");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        logger.LogInformation("Retention Worker stopped");
    }

    private async Task RunRetentionAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IWebhookEventRepository>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        // Retain processed webhook events for 30 days
        var webhookCutoff = DateTime.UtcNow.AddDays(-30);
        var deletedWebhooks = await webhookRepo.DeleteProcessedBeforeAsync(webhookCutoff, 1000, cancellationToken);
        if (deletedWebhooks > 0)
            logger.LogInformation("Retention: removed {Count} processed webhook events", deletedWebhooks);

        // Retain completed outbox messages for 7 days
        var outboxCutoff = DateTime.UtcNow.AddDays(-7);
        var deletedOutbox = await outboxRepo.DeleteCompletedBeforeAsync(outboxCutoff, 1000, cancellationToken);
        if (deletedOutbox > 0)
            logger.LogInformation("Retention: removed {Count} completed outbox messages", deletedOutbox);
    }
}
