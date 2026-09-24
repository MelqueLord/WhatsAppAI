using WhatsAppAI.Infrastructure.Workers;
using WhatsAppAI.Infrastructure.Meta.Models;

namespace WhatsAppAI.UnitTests.Webhooks;

public sealed class WebhookPhoneNumberTests
{
    [Fact]
    public void ResolveStatusFailureReason_PreservesSanitizedMetaCodeAndDetails()
    {
        var status = new WebhookStatus
        {
            Status = "failed",
            Errors =
            [
                new WebhookError
                {
                    Code = 131049,
                    Title = "Message not delivered",
                    ErrorData = new WebhookErrorData { Details = "Meta chose not to deliver this marketing message." }
                }
            ]
        };

        var result = WebhookProcessingWorker.ResolveStatusFailureReason(status);

        Assert.Equal(
            "WhatsApp error 131049: Meta chose not to deliver this marketing message.",
            result);
    }

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
