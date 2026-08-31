using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.UnitTests.Usage;

public sealed class AiResponseQuotaReservationTests
{
    [Fact]
    public void Create_StartsPendingAndPreservesIdempotencyData()
    {
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var periodStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var reservation = AiResponseQuotaReservation.Create(
            tenantId, periodStart, messageId, " response:123 ");

        Assert.Equal(tenantId, reservation.TenantId);
        Assert.Equal(periodStart, reservation.PeriodStartUtc);
        Assert.Equal(messageId, reservation.SourceMessageId);
        Assert.Equal("response:123", reservation.IdempotencyKey);
        Assert.Equal(AiResponseQuotaReservationStatus.Pending, reservation.Status);
    }

    [Fact]
    public void Commit_IsIdempotentForAlreadyCommittedReservation()
    {
        var reservation = CreateReservation();

        reservation.Commit();
        reservation.Commit();

        Assert.Equal(AiResponseQuotaReservationStatus.Committed, reservation.Status);
        Assert.NotNull(reservation.CommittedAt);
    }

    [Fact]
    public void Release_StoresSanitizedReasonAndIsIdempotent()
    {
        var reservation = CreateReservation();

        reservation.Release(" provider-timeout ");
        reservation.Release("ignored");

        Assert.Equal(AiResponseQuotaReservationStatus.Released, reservation.Status);
        Assert.Equal("provider-timeout", reservation.ReleaseReason);
        Assert.NotNull(reservation.ReleasedAt);
    }

    [Fact]
    public void CommittedReservation_CannotBeReleased()
    {
        var reservation = CreateReservation();
        reservation.Commit();

        Assert.Throws<InvalidOperationException>(() => reservation.Release("late"));
    }

    private static AiResponseQuotaReservation CreateReservation() =>
        AiResponseQuotaReservation.Create(
            Guid.NewGuid(),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            Guid.NewGuid().ToString());
}
