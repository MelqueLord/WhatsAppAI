using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class AiResponseQuotaReconciler(
    AppDbContext dbContext,
    IAiResponseQuotaService quotaService) : IAiResponseQuotaReconciler
{
    public async Task<AiResponseQuotaReconciliationResult> ReconcileAsync(
        DateTime nowUtc,
        TimeSpan reservationTimeout,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Reconciliation time must be UTC.", nameof(nowUtc));

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            reservationTimeout,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var cutoffUtc = nowUtc - reservationTimeout;
        var candidates = await dbContext.AiResponseQuotaReservations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(reservation =>
                reservation.Status == AiResponseQuotaReservationStatus.Pending &&
                reservation.CreatedAt <= cutoffUtc)
            .OrderBy(reservation => reservation.CreatedAt)
            .Take(batchSize)
            .Select(reservation => new { reservation.TenantId, reservation.Id })
            .ToListAsync(cancellationToken);

        var releasedCount = 0;
        var skippedCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                await quotaService.ReleaseAsync(
                    candidate.TenantId,
                    candidate.Id,
                    "reservation-timeout",
                    cancellationToken);
                releasedCount++;
            }
            catch (InvalidOperationException)
            {
                // A provider response may have finalized the reservation after
                // the candidate query. The next state is authoritative.
                skippedCount++;
            }
        }

        return new AiResponseQuotaReconciliationResult(
            candidates.Count,
            releasedCount,
            skippedCount);
    }
}
