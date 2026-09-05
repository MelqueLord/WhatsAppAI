using WhatsAppAI.Infrastructure.Workers;
using WhatsAppAI.Infrastructure.Meta.Models;

namespace WhatsAppAI.UnitTests.Webhooks;

public sealed class WebhookPhoneNumberTests
{
    [Fact]
    public void NormalizePhoneNumber_RestoresBrazilianMobileFromQrDeviceIdentity()
    {
        Assert.Equal("5571996531915", WebhookProcessingWorker.NormalizePhoneNumber("557196531915:0"));
    }

    [Fact]
    public void NormalizePhoneNumber_PreservesOrdinaryPhoneIdentity()
    {
        Assert.Equal("5511999990000", WebhookProcessingWorker.NormalizePhoneNumber("+55 (11) 99999-0000"));
    }

    [Fact]
    public void ResolveWhatsAppContactName_PrefersNameFromInboundMessage()
    {
        var message = new WebhookMessage
        {
            From = "5571999999999",
            PushName = "  Ana  ",
            AlternatePushName = "Outro nome"
        };

        var result = WebhookProcessingWorker.ResolveWhatsAppContactName(
            message,
            [new WebhookContact { WaId = "5571999999999", Profile = new WebhookProfile { Name = "Perfil" } }]);

        Assert.Equal("Ana", result);
    }

    [Fact]
    public void ResolveWhatsAppContactName_MatchesProfileBySenderInsteadOfUsingFirstContact()
    {
        var message = new WebhookMessage { From = "5571999999999" };

        var result = WebhookProcessingWorker.ResolveWhatsAppContactName(
            message,
            [
                new WebhookContact { WaId = "5511888888888", Profile = new WebhookProfile { Name = "Contato errado" } },
                new WebhookContact { WaId = "5571999999999", Profile = new WebhookProfile { Name = "Ana" } }
            ]);

        Assert.Equal("Ana", result);
    }

    [Fact]
    public void ResolveWhatsAppContactName_ReturnsNullWhenThereIsNoNameForSender()
    {
        var message = new WebhookMessage { From = "5571999999999" };

        var result = WebhookProcessingWorker.ResolveWhatsAppContactName(
            message,
            [new WebhookContact { WaId = "5511888888888", Profile = new WebhookProfile { Name = "Outro" } }]);

        Assert.Null(result);
    }
}
