using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class AiResponseQuotaReconciliationWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<AiResponseQuotaReconciliationWorker> logger) : BackgroundService
{
    private const int DefaultReservationTimeoutMinutes = 10;
    private const int DefaultIntervalSeconds = 60;
    private const int DefaultBatchSize = 500;
    private static readonly Meter Meter = new("WhatsAppAI.AiQuota");
    private static readonly Counter<long> ReservationsExamined =
        Meter.CreateCounter<long>("whatsappai.ai_quota.reservations.examined");
    private static readonly Counter<long> ReservationsReleased =
        Meter.CreateCounter<long>("whatsappai.ai_quota.reservations.released");
    private static readonly Counter<long> ReservationsSkipped =
        Meter.CreateCounter<long>("whatsappai.ai_quota.reservations.skipped");
    private static readonly Counter<long> ReconciliationFailures =
        Meter.CreateCounter<long>("whatsappai.ai_quota.reconciliation.failures");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reservationTimeout = TimeSpan.FromMinutes(Math.Max(
            1,
            configuration.GetValue<int?>("AiQuota:ReservationTimeoutMinutes") ??
            DefaultReservationTimeoutMinutes));
        var interval = TimeSpan.FromSeconds(Math.Max(
            5,
            configuration.GetValue<int?>("AiQuota:ReconciliationIntervalSeconds") ??
            DefaultIntervalSeconds));
        var batchSize = Math.Clamp(
            configuration.GetValue<int?>("AiQuota:ReconciliationBatchSize") ?? DefaultBatchSize,
            1,
            5000);

        logger.LogInformation(
            "AI response quota reconciliation worker started with timeout {ReservationTimeoutMinutes} minutes, interval {IntervalSeconds} seconds and batch size {BatchSize}.",
            reservationTimeout.TotalMinutes,
            interval.TotalSeconds,
            batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<IAiResponseQuotaReconciler>();
                var result = await reconciler.ReconcileAsync(
                    DateTime.UtcNow,
                    reservationTimeout,
                    batchSize,
                    stoppingToken);

                logger.LogInformation(
                    "AI response quota reconciliation completed. Examined {ExaminedCount}, released {ReleasedCount}, skipped {SkippedCount} expired reservations.",
                    result.ExaminedCount,
                    result.ReleasedCount,
                    result.SkippedCount);
                ReservationsExamined.Add(result.ExaminedCount);
                ReservationsReleased.Add(result.ReleasedCount);
                ReservationsSkipped.Add(result.SkippedCount);

                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ReconciliationFailures.Add(1);
                logger.LogError(exception, "AI response quota reconciliation failed.");

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        logger.LogInformation("AI response quota reconciliation worker stopped.");
    }
}
