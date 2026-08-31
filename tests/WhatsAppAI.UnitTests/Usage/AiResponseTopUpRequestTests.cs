using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.UnitTests.Usage;

public sealed class AiResponseTopUpRequestTests
{
    [Fact]
    public void Create_AlwaysCreatesExactlyFiveHundredPendingResponses()
    {
        var request = AiResponseTopUpRequest.Create(
            Guid.NewGuid(),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            "top-up:123",
            Guid.NewGuid());

        Assert.Equal(500, request.Quantity);
        Assert.Equal(AiResponseTopUpRequestStatus.Pending, request.Status);
    }

    [Fact]
    public void Approve_RecordsReviewerAndIsNotRepeatable()
    {
        var request = CreateRequest();
        var reviewerId = Guid.NewGuid();

        request.Approve(reviewerId);

        Assert.Equal(AiResponseTopUpRequestStatus.Approved, request.Status);
        Assert.Equal(reviewerId, request.ReviewedByUserId);
        Assert.NotNull(request.ReviewedAt);
        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void Reject_RequiresReasonAndRecordsReviewer()
    {
        var request = CreateRequest();
        var reviewerId = Guid.NewGuid();

        request.Reject(reviewerId, " manual review ");

        Assert.Equal(AiResponseTopUpRequestStatus.Rejected, request.Status);
        Assert.Equal(reviewerId, request.ReviewedByUserId);
        Assert.Equal("manual review", request.RejectionReason);
        Assert.Throws<ArgumentException>(() => CreateRequest().Reject(reviewerId, " "));
    }

    private static AiResponseTopUpRequest CreateRequest() =>
        AiResponseTopUpRequest.Create(
            Guid.NewGuid(),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid().ToString(),
            Guid.NewGuid());
}
