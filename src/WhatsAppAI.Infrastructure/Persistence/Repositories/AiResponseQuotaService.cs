using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class AiResponseQuotaService(AppDbContext dbContext) : IAiResponseQuotaService
{
    public async Task<AiResponseQuotaReservationResult> TryReserveAsync(
        Guid tenantId,
        Guid sourceMessageId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTenantAsync(tenantId, cancellationToken);

        var existing = await dbContext.AiResponseQuotaReservations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(reservation =>
                reservation.TenantId == tenantId &&
                (reservation.IdempotencyKey == idempotencyKey.Trim() ||
                    reservation.SourceMessageId == sourceMessageId),
                cancellationToken);

        if (existing is not null)
        {
            var existingSnapshot = await BuildSnapshotAsync(tenantId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AiResponseQuotaReservationResult(
                existing.Status is AiResponseQuotaReservationStatus.Pending or AiResponseQuotaReservationStatus.Committed,
                true,
                existing.Id,
                existing.Status,
                existingSnapshot);
        }

        var snapshot = await BuildSnapshotAsync(tenantId, cancellationToken);
        if (!AiResponseQuotaContract.CanReserve(
                snapshot.EffectiveLimit,
                snapshot.CommittedResponses,
                snapshot.PendingReservations))
        {
            await transaction.CommitAsync(cancellationToken);
            return new AiResponseQuotaReservationResult(false, false, null, null, snapshot);
        }

        var (periodStartUtc, _) = AiResponseQuotaContract.GetCurrentPeriod(DateTime.UtcNow);
        var reservation = AiResponseQuotaReservation.Create(
            tenantId,
            periodStartUtc,
            sourceMessageId,
            idempotencyKey);
        dbContext.AiResponseQuotaReservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var reservedSnapshot = snapshot with
        {
            PendingReservations = checked(snapshot.PendingReservations + 1)
        };
        return new AiResponseQuotaReservationResult(
            true,
            false,
            reservation.Id,
            reservation.Status,
            reservedSnapshot);
    }

    public async Task CommitAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        await MutateReservationAsync(
            tenantId,
            reservationId,
            reservation => reservation.Commit(),
            cancellationToken);
    }

    public async Task ReleaseAsync(
        Guid tenantId,
        Guid reservationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await MutateReservationAsync(
            tenantId,
            reservationId,
            reservation => reservation.Release(reason),
            cancellationToken);
    }

    public Task<AiResponseQuotaSnapshot> GetSnapshotAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        BuildSnapshotAsync(tenantId, cancellationToken);

    private async Task<AiResponseQuotaSnapshot> BuildSnapshotAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var (periodStartUtc, periodEndUtc) = AiResponseQuotaContract.GetCurrentPeriod(DateTime.UtcNow);
        var tenantLimit = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.MonthlyAiResponseLimit)
            .SingleOrDefaultAsync(cancellationToken);

        var topUps = await dbContext.UsageLedger
            .IgnoreQueryFilters()
            .Where(entry => entry.TenantId == tenantId &&
                entry.Metric == UsageMetricNames.AiResponseTopUps &&
                entry.RecordedAt >= periodStartUtc && entry.RecordedAt < periodEndUtc)
            .SumAsync(entry => (long?)entry.Quantity, cancellationToken) ?? 0;
        var effectiveLimit = AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(tenantLimit, topUps);

        var committedResponses = await dbContext.UsageLedger
            .IgnoreQueryFilters()
            .Where(entry => entry.TenantId == tenantId &&
                entry.Metric == UsageMetricNames.AiResponses &&
                entry.RecordedAt >= periodStartUtc && entry.RecordedAt < periodEndUtc)
            .SumAsync(entry => (long?)entry.Quantity, cancellationToken) ?? 0;
        var pendingReservations = await dbContext.AiResponseQuotaReservations
            .IgnoreQueryFilters()
            .Where(reservation => reservation.TenantId == tenantId &&
                reservation.PeriodStartUtc == periodStartUtc &&
                reservation.Status == AiResponseQuotaReservationStatus.Pending)
            .LongCountAsync(cancellationToken);

        return AiResponseQuotaContract.CreateSnapshot(
            effectiveLimit,
            committedResponses,
            pendingReservations);
    }

    private async Task<AiResponseQuotaReservation> GetReservationAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.AiResponseQuotaReservations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == reservationId && item.TenantId == tenantId, cancellationToken);
        return reservation ?? throw new InvalidOperationException("AI response quota reservation was not found.");
    }

    private async Task MutateReservationAsync(
        Guid tenantId,
        Guid reservationId,
        Action<AiResponseQuotaReservation> mutation,
        CancellationToken cancellationToken)
    {
        var existingTransaction = dbContext.Database.CurrentTransaction;
        IDbContextTransaction? ownedTransaction = null;
        try
        {
            if (existingTransaction is null)
                ownedTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await LockTenantAsync(tenantId, cancellationToken);
            var reservation = await GetReservationAsync(tenantId, reservationId, cancellationToken);
            mutation(reservation);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }
    }

    private async Task LockTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({tenantId.ToString()}))",
                cancellationToken);
    }
}
