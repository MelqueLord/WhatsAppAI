namespace WhatsAppAI.Application.Abstractions;

public interface IAiResponseQuotaReconciler
{
    Task<AiResponseQuotaReconciliationResult> ReconcileAsync(
        DateTime nowUtc,
        TimeSpan reservationTimeout,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public sealed record AiResponseQuotaReconciliationResult(
    int ExaminedCount,
    int ReleasedCount,
    int SkippedCount);
