using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Application.Abstractions;

public interface IAiResponseQuotaService
{
    Task<AiResponseQuotaReservationResult> TryReserveAsync(
        Guid tenantId,
        Guid sourceMessageId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task CommitAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        Guid tenantId,
        Guid reservationId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<AiResponseQuotaSnapshot> GetSnapshotAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed record AiResponseQuotaReservationResult(
    bool IsReserved,
    bool IsExisting,
    Guid? ReservationId,
    AiResponseQuotaReservationStatus? ReservationStatus,
    AiResponseQuotaSnapshot Snapshot,
    AiResponseQuotaPackageType? PackageType = null,
    string? PackageReference = null);
