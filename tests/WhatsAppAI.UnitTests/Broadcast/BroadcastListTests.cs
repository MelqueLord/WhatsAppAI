using WhatsAppAI.Domain.Broadcast;
using Xunit;

namespace WhatsAppAI.UnitTests.Broadcast;

public sealed class BroadcastListTests
{
    [Fact]
    public void Create_PersistsSelectedQueueForLaterDispatch()
    {
        var queueId = Guid.NewGuid();

        var broadcast = BroadcastList.Create(
            Guid.NewGuid(),
            "Aviso",
            "Mensagem",
            Guid.NewGuid(),
            queueId);

        Assert.Equal(queueId, broadcast.QueueId);
    }

    [Fact]
    public void StartDispatch_DoesNotReplaceTheQueueSelectedAtCreation()
    {
        var queueId = Guid.NewGuid();
        var broadcast = BroadcastList.Create(
            Guid.NewGuid(),
            "Aviso",
            "Mensagem",
            Guid.NewGuid(),
            queueId);

        broadcast.StartDispatch("qr:tenant:1:1", 1);

        Assert.Equal(queueId, broadcast.QueueId);
    }

    [Fact]
    public void UpdateMessage_AllowsDraftBroadcastsOnly()
    {
        var broadcast = BroadcastList.Create(
            Guid.NewGuid(),
            "Aviso",
            "Mensagem antiga",
            Guid.NewGuid());

        broadcast.UpdateMessage("Mensagem nova");

        Assert.Equal("Mensagem nova", broadcast.Message);
    }

    [Fact]
    public void PrepareRetry_ReopensCompletedBroadcastAndRemovesFailedCount()
    {
        var broadcast = BroadcastList.Create(
            Guid.NewGuid(),
            "Aviso",
            "Mensagem",
            Guid.NewGuid());
        broadcast.StartDispatch("qr:tenant:1:1", 2);
        broadcast.RecordSent();
        broadcast.RecordFailed();

        broadcast.PrepareRetry(1);

        Assert.Equal(BroadcastStatus.Sending, broadcast.Status);
        Assert.Equal(0, broadcast.FailedCount);
        Assert.Equal(1, broadcast.SentCount);
        Assert.Null(broadcast.FinishedAt);
    }

    [Fact]
    public void PrepareRetry_RejectsDraftBroadcasts()
    {
        var broadcast = BroadcastList.Create(
            Guid.NewGuid(),
            "Aviso",
            "Mensagem",
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => broadcast.PrepareRetry(1));
    }
}
