using WhatsAppAI.Domain.Broadcast;
using Xunit;

namespace WhatsAppAI.UnitTests.Broadcast;

public sealed class BroadcastListTests
{
    [Fact]
    public void Create_OfficialTemplate_RequiresTemplateLineAndLanguage()
    {
        Assert.Throws<ArgumentException>(() => BroadcastList.Create(
            Guid.NewGuid(), "Aviso", string.Empty, Guid.NewGuid(),
            deliveryMode: BroadcastDeliveryMode.OfficialApiTemplate));
    }

    [Fact]
    public void StartDispatch_OfficialTemplate_CannotChangeTheConfiguredLine()
    {
        var broadcast = BroadcastList.Create(Guid.NewGuid(), "Aviso", string.Empty, Guid.NewGuid(),
            deliveryMode: BroadcastDeliveryMode.OfficialApiTemplate, templateName: "status_update",
            templateLanguage: "pt_BR", linePhoneNumberId: "123");

        Assert.Throws<InvalidOperationException>(() => broadcast.StartDispatch("456", 1));
    }

    [Fact]
    public void UpdateMessage_RejectsOfficialTemplateBroadcasts()
    {
        var broadcast = BroadcastList.Create(Guid.NewGuid(), "Aviso", string.Empty, Guid.NewGuid(),
            deliveryMode: BroadcastDeliveryMode.OfficialApiTemplate, templateName: "status_update",
            templateLanguage: "pt_BR", linePhoneNumberId: "123");

        Assert.Throws<InvalidOperationException>(() => broadcast.UpdateMessage("Mensagem livre"));
    }
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
    public void StartDispatch_AcceptsQueueAudienceLargerThanManualSelectionLimit()
    {
        var broadcast = BroadcastList.Create(
            Guid.NewGuid(),
            "Oferta",
            "Mensagem",
            Guid.NewGuid(),
            Guid.NewGuid());

        broadcast.StartDispatch("qr:tenant:1:1", 501);

        Assert.Equal(BroadcastStatus.Sending, broadcast.Status);
        Assert.Equal(501, broadcast.TotalCount);
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
