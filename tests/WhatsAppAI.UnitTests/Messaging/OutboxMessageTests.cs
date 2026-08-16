using WhatsAppAI.Domain.Messaging;
using Xunit;

namespace WhatsAppAI.UnitTests.Messaging;

public class OutboxMessageTests
{
    [Fact]
    public void Create_SetsStatusPending()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(OutboxStatus.Pending, outbox.Status);
        Assert.Equal(0, outbox.RetryCount);
    }

    [Fact]
    public void MarkProcessing_SetsStatusProcessing()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();

        Assert.Equal(OutboxStatus.Processing, outbox.Status);
    }

    [Fact]
    public void MarkCompleted_SetsStatusCompleted()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkCompleted();

        Assert.Equal(OutboxStatus.Completed, outbox.Status);
        Assert.NotNull(outbox.ProcessedAt);
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCount()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkFailed("Error 1", TimeSpan.FromSeconds(10));
        outbox.MarkFailed("Error 2", TimeSpan.FromSeconds(20));

        Assert.Equal(2, outbox.RetryCount);
        Assert.Equal("Error 2", outbox.LastError);
    }

    [Fact]
    public void MarkFailed_SetsNextRetryAt()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        var before = DateTime.UtcNow;
        outbox.MarkFailed("Error", TimeSpan.FromMinutes(1));

        Assert.NotNull(outbox.NextRetryAt);
        Assert.True(outbox.NextRetryAt.Value > before);
    }

    [Fact]
    public void MarkDead_SetsStatusDead()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkDead("Max retries exceeded");

        Assert.Equal(OutboxStatus.Dead, outbox.Status);
        Assert.Equal("Max retries exceeded", outbox.LastError);
    }

    [Fact]
    public void IsReadyForProcessing_ReturnsTrueWhenPending()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(outbox.IsReadyForProcessing(DateTime.UtcNow));
    }

    [Fact]
    public void IsReadyForProcessing_ReturnsFalseWhenProcessing()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();

        Assert.False(outbox.IsReadyForProcessing(DateTime.UtcNow));
    }

    [Fact]
    public void IsReadyForProcessing_ReturnsFalseWhenNextRetryInFuture()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkFailed("Error", TimeSpan.FromHours(1));

        Assert.False(outbox.IsReadyForProcessing(DateTime.UtcNow));
    }

    [Fact]
    public async Task IsReadyForProcessing_ReturnsTrueWhenNextRetryPassed()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkFailed("Error", TimeSpan.FromMilliseconds(100));
        await Task.Delay(150);

        Assert.True(outbox.IsReadyForProcessing(DateTime.UtcNow));
    }
}
