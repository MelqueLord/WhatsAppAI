using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Application.Automation.Policy;

public sealed record AiResponseQuotaSnapshot(
    int? EffectiveLimit,
    long CommittedResponses,
    long PendingReservations)
{
    public long? AvailableResponses => EffectiveLimit is null
        ? null
        : Math.Max(0, (long)EffectiveLimit.Value - CommittedResponses - PendingReservations);

    public AiQuotaStatus Status => AiQuotaAlertPolicy.GetStatus(
        EffectiveLimit,
        checked(CommittedResponses + PendingReservations));
}

public static class AiResponseQuotaContract
{
    public static (DateTime StartUtc, DateTime EndUtc) GetCurrentPeriod(DateTime utcNow)
    {
        var instant = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : utcNow.ToUniversalTime();
        var start = new DateTime(instant.Year, instant.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    public static bool CanReserve(
        int? effectiveLimit,
        long committedResponses,
        long pendingReservations)
    {
        if (effectiveLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(effectiveLimit));
        ArgumentOutOfRangeException.ThrowIfNegative(committedResponses);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingReservations);

        return effectiveLimit is null ||
            checked(committedResponses + pendingReservations) < effectiveLimit.Value;
    }

    public static AiResponseQuotaSnapshot CreateSnapshot(
        int? effectiveLimit,
        long committedResponses,
        long pendingReservations)
    {
        _ = CanReserve(effectiveLimit, committedResponses, pendingReservations);
        return new AiResponseQuotaSnapshot(
            effectiveLimit,
            committedResponses,
            pendingReservations);
    }
}
