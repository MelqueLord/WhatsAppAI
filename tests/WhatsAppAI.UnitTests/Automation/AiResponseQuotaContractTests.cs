using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiResponseQuotaContractTests
{
    [Fact]
    public void CurrentPeriod_UsesUtcMonthBoundaries()
    {
        var period = AiResponseQuotaContract.GetCurrentPeriod(
            new DateTime(2026, 8, 31, 23, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), period.StartUtc);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), period.EndUtc);
    }

    [Fact]
    public void CurrentPeriod_ConvertsLocalInputToUtcBeforeSelectingMonth()
    {
        var local = new DateTime(2026, 8, 31, 21, 30, 0, DateTimeKind.Local);
        var period = AiResponseQuotaContract.GetCurrentPeriod(local);

        Assert.Equal(DateTimeKind.Utc, period.StartUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, period.EndUtc.Kind);
    }

    [Theory]
    [InlineData(1_500, 1_499, 0, true)]
    [InlineData(1_500, 1_499, 1, false)]
    [InlineData(1_500, 1_000, 500, false)]
    [InlineData(null, 1_000_000, 1_000_000, true)]
    public void CanReserve_AccountsForPendingReservations(
        int? limit,
        long committed,
        long pending,
        bool expected)
    {
        Assert.Equal(expected, AiResponseQuotaContract.CanReserve(limit, committed, pending));
    }

    [Fact]
    public void Snapshot_ReportsAvailableResponsesIncludingPendingReservations()
    {
        var snapshot = AiResponseQuotaContract.CreateSnapshot(1_500, 1_000, 200);

        Assert.Equal(300, snapshot.AvailableResponses);
        Assert.Equal(AiQuotaStatus.Warning, snapshot.Status);
    }

    [Fact]
    public void Snapshot_ReportsExhaustedWhenPendingReservationsConsumeLastResponse()
    {
        var snapshot = AiResponseQuotaContract.CreateSnapshot(1_500, 1_499, 1);

        Assert.Equal(0, snapshot.AvailableResponses);
        Assert.Equal(AiQuotaStatus.Exhausted, snapshot.Status);
    }

    [Fact]
    public void ReservationStatus_ContainsOnlySupportedLifecycleStates()
    {
        Assert.Equal(
            [
                AiResponseQuotaReservationStatus.Pending,
                AiResponseQuotaReservationStatus.Committed,
                AiResponseQuotaReservationStatus.Released
            ],
            Enum.GetValues<AiResponseQuotaReservationStatus>());
    }
}
